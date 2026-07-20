using ScriptUtilities;
using System.IO;
using System.Text;

namespace ScriptEditorMaui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// CAPTURADOR GLOBAL DE CRASHES
		AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
		{
			var ex = args.ExceptionObject as Exception;
			if (ex != null)
			{
				string cacheDir = FileSystem.CacheDirectory;
				string logPath = Path.Combine(cacheDir, "crash_log.txt");
				
				StringBuilder sb = new StringBuilder();
				sb.AppendLine($"Fecha: {DateTime.Now}");
				sb.AppendLine($"Mensaje: {ex.Message}");
				sb.AppendLine($"StackTrace:\n{ex.StackTrace}");
				if (ex.InnerException != null)
				{
					sb.AppendLine($"Inner Exception: {ex.InnerException.Message}");
					sb.AppendLine($"Inner StackTrace:\n{ex.InnerException.StackTrace}");
				}

				File.WriteAllText(logPath, sb.ToString());
			}
		};

		try
		{
			Settings.UserSettings = Settings.TryLoadSettings();
		}
		catch
		{
			// Fallback si falla la carga física inicial
		}

		MainPage = new ScriptEditorMaui.MainPage();
	}
}
