using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Storage;
using DivaScript;
using ScriptUtilities;

namespace ScriptEditorMaui;

public partial class MainViewModel : ObservableObject
{
	private string[]? _filePaths;

	[ObservableProperty]
	private int pvId = 0; // Por defecto pv_000 como en tu captura

	[ObservableProperty]
	private string editorText = string.Empty;

	[ObservableProperty]
	private string pvDbText = string.Empty;

	[ObservableProperty]
	private string fieldDbText = string.Empty;

	[ObservableProperty]
	private bool isBigEndian;

	[ObservableProperty]
	private bool toLower;

	[ObservableProperty]
	private bool excludeStageFilters;

	[ObservableProperty]
	private string statusMessage = string.Empty;

	[ObservableProperty]
	private bool removePvDbComments;

	private List<PvCommand> EditorCommands
	{
		get => PvScript.ParseCommandStrings(new[] { EditorText });
		set 
		{
			EditorText = ToLower
				? PvScript.CommandsToString(value).ToLower()
				: PvScript.CommandsToString(value);
		}
	}

	// ==========================================
	// LÓGICA DE MENÚS DESPLEGABLES INTERACTIVOS
	// ==========================================

	[RelayCommand]
	private async Task MenuFile()
	{
		var choice = await Shell.Current.DisplayActionSheet("File", "Cancelar", null, "Open Script/DSC");
		if (choice == "Open Script/DSC")
		{
			await OpenFileAsync(null);
		}
	}

	[RelayCommand]
	private async Task MenuEdit()
	{
		var choice = await Shell.Current.DisplayActionSheet(
			"Edit Options", 
			"Cancelar", 
			null, 
			"Open F 2nd Edit as F script",
			"Open F 2nd Edit as FT script",
			"F 2nd Edit -> FT PokeSlow",
			"DIVA 2nd -> FT PokeSlow",
			"DIVA Extend -> FT",
			$"Change PV ID (Actual: pv_{PvId:D3})");

		if (choice == "Cancelar" || choice is null) return;

		if (choice.StartsWith("Change PV ID"))
		{
			string result = await Shell.Current.DisplayPromptAsync("PV ID", "Introduce el número de PV (ej: 0 para pv_000, 999 para pv_999):", initialValue: PvId.ToString(), keyboard: Keyboard.Numeric);
			if (int.TryParse(result, out int newId))
			{
				PvId = newId;
				StatusMessage = $"PV ID cambiado a pv_{PvId:D3}";
			}
			return;
		}

		// Si seleccionó abrir un Edit, disparamos el buscador de archivos pasándole el modo
		await OpenFileAsync(choice);
	}

	[RelayCommand]
	private async Task MenuExport()
	{
		var choice = await Shell.Current.DisplayActionSheet(
			"Export Script", 
			"Cancelar", 
			null, 
			"Save as DivaScript", 
			"Save as F DSC", 
			"Save as F2 DSC", 
			"Save as FT DSC", 
			"Save as X DSC", 
			"Save as MGF DSC", 
			"Save as Mirai DSC");

		if (choice == "Cancelar" || choice is null) return;

		Format? formatoExportar = choice switch
		{
			"Save as DivaScript" => Format.DivaScript,
			"Save as F DSC" => Format.F,
			"Save as F2 DSC" => Format.F2,
			"Save as FT DSC" => Format.FT,
			"Save as X DSC" => Format.X,
			"Save as MGF DSC" => Format.MGF,
			"Save as Mirai DSC" => Format.Mirai,
			_ => null
		};

		if (formatoExportar != null)
		{
			await SaveFileWithFormatAsync(formatoExportar.Value);
		}
	}

	[RelayCommand]
	private async Task MenuTools()
	{
		var choice = await Shell.Current.DisplayActionSheet(
			"Tools", 
			"Cancelar", 
			null, 
			"Remove Unused Time Commands",
			"Add Timestamp Comments",
			"Add F2 Target Type Comments",
			"Add FT Target Type Comments",
			"Toggle Remove PVDb Comments");

		if (choice == "Remove Unused Time Commands") RemoveUnusedTimeCommands();
		else if (choice == "Toggle Remove PVDb Comments")
		{
			RemovePvDbComments = !RemovePvDbComments;
			await Shell.Current.DisplayAlert("Tools", $"Remove PVDb Comments ahora está: {(RemovePvDbComments ? "ACTIVADO" : "DESACTIVADO")}", "OK");
		}
	}

	// ==========================================
	// PROCESAMIENTO DE ARCHIVOS Y BINARIOS SECURE.BIN
	// ==========================================

	private async Task OpenFileAsync(string editMode)
	{
		try
		{
			var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
			{
				PickerTitle = editMode ?? "Selecciona archivo(s) DSC o DivaScript"
			});

			var paths = results?.Select(r => r.FullPath).ToArray();
			if (paths is null || paths.Length == 0) return;

			_filePaths = paths;

			if (editMode != null)
			{
				// Flujo especializado para SECURE.BIN o carpetas de Edits
				StatusMessage = $"Procesando Edit: {Path.GetFileName(paths[0])}...";
				
				// Cargamos el archivo usando el parser de edits de la librería nativa
				var bytes = await File.ReadAllBytesAsync(paths[0]);
				Edit miEdit = DivaScript.EditParser.LoadFromBin(bytes);
				PvDatabaseInfo pvInfo = new PvDatabaseInfo(miEdit, PvId);

				// Seteamos las tres vistas según la conversión elegida
				EditorCommands = miEdit.GetScriptCommands();
				
				string pvText = string.Join(Environment.NewLine, pvInfo.GetFtPvDb());
				string fieldText = string.Join(Environment.NewLine, pvInfo.GetFtFieldDb());

				if (RemovePvDbComments)
				{
					pvText = string.Join(Environment.NewLine, pvText.Split('\n').Where(l => !l.Trim().StartsWith("#")));
					fieldText = string.Join(Environment.NewLine, fieldText.Split('\n').Where(l => !l.Trim().StartsWith("#")));
				}

				PvDbText = pvText;
				FieldDbText = fieldText;

				StatusMessage = $"Edit cargado con éxito para pv_{PvId:D3}";
			}
			else
			{
				// Flujo estándar para scripts individuales sueltos (.dsc / .txt)
				var choice = await Shell.Current.DisplayActionSheet(
					"Elige el formato de entrada", "Cancelar", null,
					"DivaScript (.txt)", "F", "F 2nd", "Future Tone", "X", "MGF", "Mirai");

				Format? format = choice switch
				{
					"DivaScript (.txt)" => Format.DivaScript,
					"F" => Format.F,
					"F 2nd" => Format.F2,
					"Future Tone" => Format.FT,
					"X" => Format.X,
					"MGF" => Format.MGF,
					"Mirai" => Format.Mirai,
					_ => null
				};

				if (format is null) return;

				if (format == Format.DivaScript) EditorCommands = PvScript.ParseTextFiles(_filePaths);
				else EditorCommands = PvScript.ParseBinaryScripts(_filePaths, format.Value, IsBigEndian);

				StatusMessage = $"Abierto: {string.Join(", ", paths.Select(Path.GetFileName))}";
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error al cargar: {ex.Message}";
		}
	}

	private async Task SaveFileWithFormatAsync(Format format)
	{
		try
		{
			var commands = EditorCommands;
			if (ExcludeStageFilters)
			{
				commands = commands
					.Where(c => c.Opcode.ToUpper() is not ("TONE_TRANS" or "SATURATE" or "PSE"))
					.ToList();
			}

			var suggestedName = GuessFileNameFromPvDb() ?? $"pv_{PvId:D3}.dsc";

			using var stream = new MemoryStream();
			if (format == Format.DivaScript)
			{
				var writer = new StreamWriter(stream, leaveOpen: true);
				writer.Write(PvScript.CommandsToString(commands));
				writer.Flush();
			}
			else
			{
				var tempPath = Path.Combine(FileSystem.CacheDirectory, suggestedName);
				PvScript.WriteBinaryScript(tempPath, commands, format, IsBigEndian);
				stream.Write(await File.ReadAllBytesAsync(tempPath));
				stream.Position = 0;
			}

			stream.Position = 0;
			await FileSaver.Default.SaveAsync(suggestedName, stream);
			StatusMessage = $"Exportado: {suggestedName}";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error al guardar: {ex.Message}";
		}
	}

	private string? GuessFileNameFromPvDb()
	{
		if (string.IsNullOrEmpty(PvDbText)) return null;
		var line = PvDbText.Split('\n').FirstOrDefault(l => l.Contains("script_file_name="));
		if (line is null) return null;
		var parts = line.Split('=', 2);
		return parts.Length > 1 ? Path.GetFileName(parts[1].Trim()) : null;
	}

	// ==========================================
	// ACCIONES DEL PANEL INFERIOR
	// ==========================================

	[RelayCommand]
	private void AddTargetFlyingTimeCommands()
		=> EditorCommands = CommandFormatting.AddTargetFlyingTimeCommands(EditorCommands);

	[RelayCommand]
	private void AutoFormatF2ToFt()
		=> EditorCommands = CommandFormatting.SmartReformatFtoFt(EditorCommands);

	[RelayCommand]
	private void AutoFormatFtToF2()
		=> EditorCommands = CommandFormatting.SmartReformatFtToF(EditorCommands, false);

	[RelayCommand]
	private void AutoFormatFtToF2Vita()
		=> EditorCommands = CommandFormatting.SmartReformatFtToF(EditorCommands, true);

	[RelayCommand]
	private async Task RemoveChartAsync()
	{
		bool confirm = await Shell.Current.DisplayAlert("Advertencia", "Vas a eliminar todos los comandos del chart.\n¿Continuar?", "Sí", "No");
		if (!confirm) return;

		string[] chartOpcodes = { "TARGET", "TARGET_FLYING_TIME", "BAR_TIME_SET", "MUSIC_PLAY" };
		EditorCommands = EditorCommands.Where(c => !chartOpcodes.Contains(c.Opcode.ToUpper())).ToList();
	}

	[RelayCommand]
	private void ReformatFTargets()
		=> EditorCommands = CommandFormatting.ReformatFTargetList(EditorCommands);

	[RelayCommand]
	private void ReformatFtTargets()
		=> EditorCommands = CommandFormatting.ReformatFtTargetList(EditorCommands);

	[RelayCommand]
	private void RemoveUnusedTimeCommands()
		=> EditorCommands = PvScript.OrderListByTime(EditorCommands).Where(c => c.Opcode.ToUpper() != "TIME").ToList();
}
