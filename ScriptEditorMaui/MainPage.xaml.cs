namespace ScriptEditorMaui;

public partial class MainPage : TabbedPage
{
	public MainPage()
	{
		InitializeComponent();
		this.Loaded += MainPage_Loaded;
	}

	private async void MainPage_Loaded(object? sender, EventArgs e)
	{
		this.Loaded -= MainPage_Loaded;

		string logPath = Path.Combine(FileSystem.CacheDirectory, "crash_log.txt");
		if (File.Exists(logPath))
		{
			string content = await File.ReadAllTextAsync(logPath);
			await DisplayAlert("Último crash detectado", content, "OK");
			File.Delete(logPath);
		}
	}
}
