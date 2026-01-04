using System.CommandLine;
using KopioRapido.Models;

namespace KopioRapido.CLI.Commands;

public class BiDirectionalSyncCommand : BaseCommand
{
    public static Command Create(IServiceProvider services, GlobalOptions globalOptions)
    {
        var cmd = new Command("bidirectional-sync", "Two-way sync (newer timestamp wins)");

        var path1Arg = new Argument<string>("path1");
        path1Arg.Description = "First directory path";
        var path2Arg = new Argument<string>("path2");
        path2Arg.Description = "Second directory path";

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

        cmd.Arguments.Add(path1Arg);
        cmd.Arguments.Add(path2Arg);
        cmd.Options.Add(analyzeOption);
        cmd.Options.Add(strategyOption);
        cmd.Options.Add(maxConcurrentOption);
        cmd.Options.Add(bufferSizeOption);
        cmd.Options.Add(noCompressionOption);
        cmd.Options.Add(noDeltaSyncOption);

        cmd.SetAction(result =>
        {
            var path1 = result.GetValue(path1Arg) ?? "";
            var path2 = result.GetValue(path2Arg) ?? "";
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

            var command = new BiDirectionalSyncCommand(services);
            return command.ExecuteAsync(
                path1, path2, analyze, strategy, maxConcurrent, bufferSize,
                noCompression, noDeltaSync, verbose, json, plain, color, stateDir
            ).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private BiDirectionalSyncCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(
        string path1, string path2, bool analyze, string? strategy,
        int? maxConcurrent, int? bufferSize, bool noCompression, bool noDeltaSync,
        bool verbose, bool json, bool plain, bool color, string? stateDir)
    {
        return await ExecuteOperationAsync(
            path1, path2, CopyOperationType.BiDirectionalSync,
            analyze, noCompression, noDeltaSync,
            strategy, maxConcurrent, bufferSize,
            verbose, json, plain, color, stateDir,
            CancellationToken.None);
    }
}
