using System.CommandLine;
using KopioRapido.Services;
using KopioRapido.Models;
using KopioRapido.CLI.Output;
using Microsoft.Extensions.DependencyInjection;

namespace KopioRapido.CLI.Commands;

public abstract class BaseCommand
{
    private readonly IServiceProvider _defaultServices;
    protected IServiceProvider Services { get; private set; }
    protected IFileOperationService FileOpService { get; private set; }

    protected BaseCommand(IServiceProvider services)
    {
        _defaultServices = services;
        Services = services;
        FileOpService = services.GetRequiredService<IFileOperationService>();
    }
    
    protected void ConfigureStateDirectory(string? stateDir)
    {
        if (!string.IsNullOrEmpty(stateDir))
        {
            // Validate the directory exists or can be created
            if (!Directory.Exists(stateDir))
            {
                Directory.CreateDirectory(stateDir);
            }
            
            // Create new service provider with custom state directory
            Services = ServiceConfiguration.ConfigureServices(stateDir);
            FileOpService = Services.GetRequiredService<IFileOperationService>();
        }
    }

    protected async Task<int> ExecuteOperationAsync(
        string source,
        string destination,
        CopyOperationType operationType,
        bool analyze,
        bool noCompression,
        bool noDeltaSync,
        string? strategy,
        int? maxConcurrent,
        int? bufferSize,
        bool verbose,
        bool json,
        bool plain,
        bool color,
        string? stateDir,
        CancellationToken cancellationToken)
    {
        // Configure custom state directory if specified
        ConfigureStateDirectory(stateDir);

        var outputFormatter = json ?
            (IOutputFormatter)new JsonOutputFormatter() :
            new ConsoleOutputFormatter(verbose, plain, color);

        try
        {
            // Validate paths
            if (!Directory.Exists(source))
            {
                outputFormatter.Error($"Source path does not exist: {source}");
                return 1;
            }

            // Analyze strategy
            var (sourceProfile, destProfile, fileProfile, selectedStrategy) =
                await FileOpService.AnalyzeAndSelectStrategyAsync(source, destination, cancellationToken);

            // Override strategy if specified
            if (!string.IsNullOrEmpty(strategy) || maxConcurrent.HasValue ||
                noCompression || noDeltaSync || bufferSize.HasValue)
            {
                selectedStrategy = BuildCustomStrategy(
                    strategy, maxConcurrent, bufferSize,
                    !noCompression && selectedStrategy.UseCompression,
                    !noDeltaSync);
            }

            outputFormatter.ShowAnalysis(sourceProfile, destProfile, fileProfile, selectedStrategy);

            if (analyze)
            {
                // Dry-run mode - show what would happen
                var syncSummary = await FileOpService.AnalyzeSyncAsync(
                    source, destination, operationType, cancellationToken);
                outputFormatter.ShowSyncSummary(syncSummary);
                return 0;
            }

            // Execute operation
            var progress = new Progress<FileTransferProgress>(p =>
                outputFormatter.ShowProgress(p));

            var operation = await FileOpService.StartOperationAsync(
                source, destination, operationType,
                progress, cancellationToken, selectedStrategy);

            outputFormatter.ShowResult(operation);

            return operation.Status == CopyStatus.Completed ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            outputFormatter.Warning("Operation cancelled by user");
            return 130; // Standard cancellation exit code
        }
        catch (Exception ex)
        {
            outputFormatter.Error($"Error: {ex.Message}");
            if (verbose)
                outputFormatter.Error(ex.StackTrace ?? "");
            return 1;
        }
    }

    private TransferStrategy BuildCustomStrategy(
        string? mode, int? maxConcurrent, int? bufferSize,
        bool useCompression, bool useDeltaSync)
    {
        var strategy = mode?.ToLower() switch
        {
            "sequential" => TransferStrategy.Sequential("User specified"),
            "conservative" => TransferStrategy.ParallelConservative("User specified"),
            "moderate" => TransferStrategy.ParallelModerate("User specified"),
            "aggressive" => TransferStrategy.ParallelAggressive("User specified"),
            _ => TransferStrategy.ParallelModerate("Default")
        };

        if (maxConcurrent.HasValue)
            strategy.MaxConcurrentFiles = maxConcurrent.Value;
        if (bufferSize.HasValue)
            strategy.BufferSizeKB = bufferSize.Value;

        strategy.UseCompression = useCompression;
        strategy.UseDeltaSync = useDeltaSync;

        return strategy;
    }
}
