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
	private int _pvId = 999;

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

	private List<PvCommand> EditorCommands
	{
		get => PvScript.ParseCommandStrings(new[] { EditorText });
		set => EditorText = ToLower
			? PvScript.CommandsToString(value).ToLower()
			: PvScript.CommandsToString(value);
	}

	[RelayCommand]
	private async Task OpenFileAsync()
	{
		try
		{
			var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
			{
				PickerTitle = "Selecciona archivo(s) DSC o DivaScript"
			});

			var paths = results?.Select(r => r.FullPath).ToArray();
			if (paths is null || paths.Length == 0) return;

			_filePaths = paths;

			var format = await ChooseFormatAsync();
			if (format is null) return;

			if (format == Format.DivaScript)
			{
				EditorCommands = PvScript.ParseTextFiles(_filePaths);
			}
			else
			{
				EditorCommands = PvScript.ParseBinaryScripts(_filePaths, format.Value, IsBigEndian);
			}

			StatusMessage = $"Abierto: {string.Join(", ", paths.Select(Path.GetFileName))}";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error al abrir: {ex.Message}";
		}
	}

	[RelayCommand]
	private async Task SaveFileAsync()
	{
		try
		{
			var format = await ChooseFormatAsync();
			if (format is null) return;

			var commands = EditorCommands;
			if (ExcludeStageFilters)
			{
				commands = commands
					.Where(c => c.Opcode.ToUpper() is not ("TONE_TRANS" or "SATURATE" or "PSE"))
					.ToList();
			}

			var suggestedName = GuessFileNameFromPvDb() ?? "script.dsc";

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
				PvScript.WriteBinaryScript(tempPath, commands, format.Value, IsBigEndian);
				stream.Write(await File.ReadAllBytesAsync(tempPath));
				stream.Position = 0;
			}

			stream.Position = 0;
			await FileSaver.Default.SaveAsync(suggestedName, stream);
			StatusMessage = $"Guardado: {suggestedName}";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error al guardar: {ex.Message}";
		}
	}

	private async Task<Format?> ChooseFormatAsync()
	{
		var choice = await Shell.Current.DisplayActionSheet(
			"Elige el formato",
			"Cancelar",
			null,
			"DivaScript (.txt)", "F", "F 2nd", "Future Tone", "X", "MGF", "Mirai");

		return choice switch
		{
			"DivaScript (.txt)" => Format.DivaScript,
			"F" => Format.F,
			"F 2nd" => Format.F2,
			"Future Tone" => Format.FT,
			"X" => Format.X,
			"MGF" => Format.MGF,
			"Mirai" => Format.Mirai,
			_ => (Format?)null
		};
	}

	private string? GuessFileNameFromPvDb()
	{
		var line = PvDbText.Split('\n').FirstOrDefault(l => l.Contains("script_file_name="));
		if (line is null) return null;
		var parts = line.Split('=', 2);
		return parts.Length > 1 ? Path.GetFileName(parts[1]) : null;
	}

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
		bool confirm = await Shell.Current.DisplayAlert(
			"Advertencia",
			"Vas a eliminar todos los comandos relacionados con el chart.\n¿Continuar?",
			"Sí", "No");
		if (!confirm) return;

		string[] chartOpcodes = { "TARGET", "TARGET_FLYING_TIME", "BAR_TIME_SET", "MUSIC_PLAY" };
		EditorCommands = EditorCommands
			.Where(c => !chartOpcodes.Contains(c.Opcode.ToUpper()))
			.ToList();
	}

	[RelayCommand]
	private void ReformatFTargets()
		=> EditorCommands = CommandFormatting.ReformatFTargetList(EditorCommands);

	[RelayCommand]
	private void ReformatFtTargets()
		=> EditorCommands = CommandFormatting.ReformatFtTargetList(EditorCommands);

	[RelayCommand]
	private void RemoveUnusedTimeCommands()
		=> EditorCommands = PvScript.OrderListByTime(EditorCommands)
			.Where(c => c.Opcode.ToUpper() != "TIME")
			.ToList();
}
