using ScriptUtilities;

namespace ScriptEditorMaui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Blindamos la carga física para evitar el insta-crash en Android
		try
		{
			Settings.UserSettings = Settings.TryLoadSettings();
		}
		catch
		{
			// Si lanza una excepción por falta de permisos o rutas IO,
			// dejamos que continúe el inicio normal de la app.
		}

		MainPage = new AppShell();
	}
}
