using ScriptUtilities;

namespace ScriptEditorMaui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		Settings.UserSettings = Settings.TryLoadSettings();

		MainPage = new AppShell();
	}
}
