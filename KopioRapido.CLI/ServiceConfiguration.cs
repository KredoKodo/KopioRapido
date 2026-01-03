using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KopioRapido.Services;
using KopioRapido.Core;

namespace KopioRapido.CLI;

public static class ServiceConfiguration
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core Services (from KopioRapido.Core)
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IProgressTrackerService, ProgressTrackerService>();
        services.AddSingleton<IResumeService, ResumeService>();
        services.AddSingleton<IPerformanceMonitorService, PerformanceMonitorService>();
        services.AddSingleton<IQuickScanService, QuickScanService>();

        // Core Engines
        services.AddSingleton<FileCopyEngine>();
        services.AddSingleton<FileComparisonHelper>();
        services.AddSingleton<StorageProfiler>();
        services.AddSingleton<FileAnalyzer>();
        services.AddSingleton<TransferIntelligenceEngine>();

        // High-level Service
        services.AddSingleton<IFileOperationService, FileOperationService>();

        return services.BuildServiceProvider();
    }
}
