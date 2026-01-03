using Microsoft.Extensions.Logging;
using KopioRapido.Core;
using KopioRapido.Services;
using KopioRapido.ViewModels;

namespace KopioRapido;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		DiagnosticLogger.Log("=== CreateMauiApp START ===");

		try
		{
			DiagnosticLogger.Log("Creating MauiApp builder...");
			var builder = MauiApp.CreateBuilder();

			DiagnosticLogger.Log("Configuring UseMauiApp...");
			builder.UseMauiApp<App>();

			DiagnosticLogger.Log("Configuring fonts...");
			builder.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
			DiagnosticLogger.Log("Adding debug logging...");
			builder.Logging.AddDebug();
#endif

			// Register core services
			DiagnosticLogger.Log("Registering ILoggingService...");
			builder.Services.AddSingleton<ILoggingService, LoggingService>();

			DiagnosticLogger.Log("Registering IProgressTrackerService...");
			builder.Services.AddSingleton<IProgressTrackerService, ProgressTrackerService>();

			DiagnosticLogger.Log("Registering IResumeService...");
			builder.Services.AddSingleton<IResumeService, ResumeService>();

			DiagnosticLogger.Log("Registering FileCopyEngine...");
			builder.Services.AddSingleton<FileCopyEngine>();

			DiagnosticLogger.Log("Registering IFileOperationService...");
			builder.Services.AddSingleton<IFileOperationService, FileOperationService>();

			// Register platform-specific folder picker service
#if WINDOWS
			DiagnosticLogger.Log("Registering Windows FolderPickerService...");
			builder.Services.AddSingleton<IFolderPickerService, Platforms.Windows.FolderPickerService>();
#elif MACCATALYST
			DiagnosticLogger.Log("Registering MacCatalyst FolderPickerService...");
			builder.Services.AddSingleton<IFolderPickerService, Platforms.MacCatalyst.FolderPickerService>();
#endif

			// Register ViewModels
			DiagnosticLogger.Log("Registering MainViewModel...");
			builder.Services.AddTransient<MainViewModel>();

			// Register Pages
			DiagnosticLogger.Log("Registering MainPage...");
			builder.Services.AddTransient<MainPage>();

			DiagnosticLogger.Log("Building MauiApp...");
			var app = builder.Build();

			DiagnosticLogger.Log("=== CreateMauiApp SUCCESS ===");
			return app;
		}
		catch (Exception ex)
		{
			DiagnosticLogger.LogException("CreateMauiApp", ex);
			throw;
		}
	}
}
