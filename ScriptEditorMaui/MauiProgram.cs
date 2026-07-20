using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using System.Text; // Asegúrate de tener esta directiva para codificación

namespace ScriptEditorMaui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// REGISTRO DE CODE PAGES PARA SOPORTAR SHIFT-JIS (JAPONÉS 932)
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		// Registro de vistas y ViewModels obligatorios
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<MainViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
