using Microsoft.Extensions.Logging;
using KopioRapido.Core;
using KopioRapido.Services;
using KopioRapido.ViewModels;

namespace KopioRapido;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Register core services
		builder.Services.AddSingleton<ILoggingService, LoggingService>();
		builder.Services.AddSingleton<IProgressTrackerService, ProgressTrackerService>();
		builder.Services.AddSingleton<IResumeService, ResumeService>();
		builder.Services.AddSingleton<FileCopyEngine>();
		builder.Services.AddSingleton<IFileOperationService, FileOperationService>();

		// Register platform-specific folder picker service
#if WINDOWS
		builder.Services.AddSingleton<IFolderPickerService, Platforms.Windows.FolderPickerService>();
#elif MACCATALYST
		builder.Services.AddSingleton<IFolderPickerService, Platforms.MacCatalyst.FolderPickerService>();
#endif

		// Register ViewModels
		builder.Services.AddTransient<MainViewModel>();

		// Register Pages
		builder.Services.AddTransient<MainPage>();

		return builder.Build();
	}
}
