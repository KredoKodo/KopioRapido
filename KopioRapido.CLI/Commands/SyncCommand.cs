using System.CommandLine;
using KopioRapido.Models;

namespace KopioRapido.CLI.Commands;

public class SyncCommand : BaseCommand
{
    public static Command Create(IServiceProvider services, GlobalOptions globalOptions)
    {
        var cmd = new Command("sync", "One-way sync (only copies missing/newer files)");

        var sourceArg = new Argument<string>("source");
        sourceArg.Description = "Source directory path";
        var destArg = new Argument<string>("destination");
        destArg.Description = "Destination directory path";

        // Command-specific options
        var analyzeOption = new Option<bool>("--analyze");
        analyzeOption.Description = "Analyze without syncing (dry-run)";

        var strategyOption = new Option<string?>("--strategy");
        strategyOption.Description = "Transfer strategy: sequential|conservative|moderate|aggressive";

        var maxConcurrentOption = new Option<int?>("--max-concurrent");
        maxConcurrentOption.Description = "Maximum concurrent file transfers (overrides strategy)";

        var bufferSizeOption = new Option<int?>("--buffer-size");
        bufferSizeOption.Description = "Buffer size in KB (overrides strategy)";

        var noCompressionOption = new Option<bool>("--no-compression");
        noCompressionOption.Description = "Disable compression for network transfers";

        var noDeltaSyncOption = new Option<bool>("--no-delta-sync");
        noDeltaSyncOption.Description = "Disable delta sync for large files";

        cmd.Arguments.Add(sourceArg);
        cmd.Arguments.Add(destArg);
        cmd.Options.Add(analyzeOption);
        cmd.Options.Add(strategyOption);
        cmd.Options.Add(maxConcurrentOption);
        cmd.Options.Add(bufferSizeOption);
        cmd.Options.Add(noCompressionOption);
        cmd.Options.Add(noDeltaSyncOption);

        cmd.SetAction(result =>
        {
            var source = result.GetValue(sourceArg) ?? "";
            var dest = result.GetValue(destArg) ?? "";
            var analyze = result.GetValue(analyzeOption);
            var strategy = result.GetValue(strategyOption);
            var maxConcurrent = result.GetValue(maxConcurrentOption);
            var bufferSize = result.GetValue(bufferSizeOption);
            var noCompression = result.GetValue(noCompressionOption);
            var noDeltaSync = result.GetValue(noDeltaSyncOption);

            // Get global options
            var verbose = result.GetValue(globalOptions.Verbose);
            var json = result.GetValue(globalOptions.Json);
            var plain = result.GetValue(globalOptions.Plain);
            var color = result.GetValue(globalOptions.Color);
            var stateDir = result.GetValue(globalOptions.StateDir);

            var command = new SyncCommand(services);
            return command.ExecuteAsync(
                source, dest, analyze, strategy, maxConcurrent, bufferSize,
                noCompression, noDeltaSync, verbose, json, plain, color, stateDir
            ).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private SyncCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(
        string source, string dest, bool analyze, string? strategy,
        int? maxConcurrent, int? bufferSize, bool noCompression, bool noDeltaSync,
        bool verbose, bool json, bool plain, bool color, string? stateDir)
    {
        return await ExecuteOperationAsync(
            source, dest, CopyOperationType.Sync,
            analyze, noCompression, noDeltaSync,
            strategy, maxConcurrent, bufferSize,
            verbose, json, plain, color, stateDir,
            CancellationToken.None);
    }
}
