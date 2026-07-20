using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
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
	private int pvId = 0;

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
	private string statusMessage = "Listo.";

	[ObservableProperty]
	private bool removePvDbComments;

	private List<PvCommand> EditorCommands
	{
		get
		{
			if (string.IsNullOrWhiteSpace(EditorText))
			{
				return new List<PvCommand>();
			}

			try
			{
				return PvScript.ParseCommandStrings(new[] { EditorText }) ?? new List<PvCommand>();
			}
			catch
			{
				return new List<PvCommand>();
			}
		}
		set 
		{
			if (value is null) return;

			try
			{
				EditorText = ToLower
					? PvScript.CommandsToString(value).ToLower()
					: PvScript.CommandsToString(value);
			}
			catch (Exception ex)
			{
				StatusMessage = $"Error al formatear texto: {ex.Message}";
			}
		}
	}

	[RelayCommand]
	private async Task MenuFile()
	{
		var choice = await Application.Current!.MainPage!.DisplayActionSheet("File", "Cancelar", null, "Open Script/DSC");
		if (choice == "Open Script/DSC")
		{
			await OpenFileAsync(null);
		}
	}

	[RelayCommand]
	private async Task MenuEdit()
	{
		var choice = await Application.Current!.MainPage!.DisplayActionSheet(
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
			string result = await Application.Current!.MainPage!.DisplayPromptAsync("PV ID", "Introduce el número de PV (ej: 0 para pv_000, 999 para pv_999):", initialValue: PvId.ToString(), keyboard: Keyboard.Numeric);
			if (int.TryParse(result, out int newId))
			{
				PvId = newId;
				StatusMessage = $"PV ID cambiado a pv_{PvId:D3}";
			}
			return;
		}

		await OpenFileAsync(choice);
	}

	[RelayCommand]
	private async Task MenuExport()
	{
		var choice = await Application.Current!.MainPage!.DisplayActionSheet(
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
		var choice = await Application.Current!.MainPage!.DisplayActionSheet(
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
			await Application.Current!.MainPage!.DisplayAlert("Tools", $"Remove PVDb Comments ahora está: {(RemovePvDbComments ? "ACTIVADO" : "DESACTIVADO")}", "OK");
		}
	}

	private async Task OpenFileAsync(string? editMode)
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
				StatusMessage = $"Procesando Edit: {Path.GetFileName(paths[0])}...";

				List<PvCommand> commands;
				PvDatabaseInfo pvDatabase;

				// Forzamos el proveedor Shift-JIS (Code Page 932) para evitar textos corruptos
				var shiftJis = Encoding.GetEncoding(932);

				if (editMode.Contains("DIVA Extend"))
				{
					// Se le inyecta el encoding al constructor para que el binario se lea correctamente
					var edit = new EditMode.DivaExtend.Edit(paths[0], shiftJis);
					(commands, pvDatabase) = DivaExtendEditScript.GetFtEditCommands(edit, PvId);
				}
				else
				{
					// Se le inyecta el encoding al constructor para que el binario se lea correctamente
					var edit = new EditMode.Diva2nd.Edit(paths[0], shiftJis);
					(commands, pvDatabase) = Diva2ndEditScript.GetFtEditCommands(edit, PvId);
				}

				EditorCommands = commands;

				var pvDbLines = pvDatabase.GetFtPvDb();
				var fieldDbLines = pvDatabase.GetFtFieldDb();

				if (RemovePvDbComments)
				{
					pvDbLines = pvDbLines.Where(l => !l.TrimStart().StartsWith("#")).ToList();
					fieldDbLines = fieldDbLines.Where(l => !l.TrimStart().StartsWith("#")).ToList();
				}

				PvDbText = string.Join(Environment.NewLine, pvDbLines);
				FieldDbText = string.Join(Environment.NewLine, fieldDbLines);

				StatusMessage = $"Edit cargado con éxito para pv_{PvId:D3}";
			}
			else
			{
				var choice = await Application.Current!.MainPage!.DisplayActionSheet(
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
			await Application.Current!.MainPage!.DisplayAlert("Error al cargar", $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}", "OK");
		}
	}

	private async Task SaveFileWithFormatAsync(Format format)
	{
		try
		{
			var commands = EditorCommands;
			if (commands is null || commands.Count == 0)
			{
				await Application.Current!.MainPage!.DisplayAlert("Exportar", "No hay comandos en el script actual para exportar.", "OK");
				return;
			}

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

	[RelayCommand]
	private void AddTargetFlyingTimeCommands()
	{
		if (string.IsNullOrWhiteSpace(EditorText)) return;
		EditorCommands = CommandFormatting.AddTargetFlyingTimeCommands(EditorCommands);
	}

	[RelayCommand]
	private void AutoFormatF2ToFt()
	{
		if (string.IsNullOrWhiteSpace(EditorText)) return;
		EditorCommands = CommandFormatting.SmartReformatFtoFt(EditorCommands);
	}

	[RelayCommand]
	private void AutoFormatFtToF2()
	{
		if (string.IsNullOrWhiteSpace(EditorText)) return;
		EditorCommands = CommandFormatting.SmartReformatFtToF(EditorCommands, false);
	}

	[RelayCommand]
	private void AutoFormatFtToF2Vita()
	{
		if (string.IsNullOrWhiteSpace(EditorText)) return;
		EditorCommands = CommandFormatting.SmartReformatFtToF(EditorCommands, true);
	}

	[RelayCommand]
	private async Task RemoveChartAsync()
	{
		if (string.IsNullOrWhiteSpace(EditorText)) return;

		bool confirm = await Application.Current!.MainPage!.DisplayAlert("Advertencia", "Vas a eliminar todos los comandos del chart.\n¿Continuar?", "Sí", "No");
		if (!confirm) return;

		string[] chartOpcodes = { "TARGET", "TARGET_FLYING_TIME", "BAR_TIME_SET", "MUSIC_PLAY" };
		EditorCommands = EditorCommands.Where(c => !chartOpcodes.Contains(c.Opcode.ToUpper())).ToList();
	}

	[RelayCommand]
	private void ReformatFTargets()
	{
		if (string.IsNullOrWhiteSpace(EditorText)) return;
		EditorCommands = CommandFormatting.ReformatFTargetList(EditorCommands);
	}

	[RelayCommand]
	private void ReformatFtTargets()
	{
		if (string.IsNullOrWhiteSpace(EditorText)) return;
		EditorCommands = CommandFormatting.ReformatFtTargetList(EditorCommands);
	}

	[RelayCommand]
	private void RemoveUnusedTimeCommands()
	{
		if (string.IsNullOrWhiteSpace(EditorText)) return;
		EditorCommands = PvScript.OrderListByTime(EditorCommands).Where(c => c.Opcode.ToUpper() != "TIME").ToList();
	}
}
