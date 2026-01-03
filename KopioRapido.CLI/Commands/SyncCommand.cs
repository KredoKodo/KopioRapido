using System.CommandLine;
using KopioRapido.Models;

namespace KopioRapido.CLI.Commands;

public class SyncCommand : BaseCommand
{
    public static Command Create(IServiceProvider services)
    {
        var cmd = new Command("sync", "One-way sync (only copies missing/newer files)");

        var sourceArg = new Argument<string>("source");
        sourceArg.Description = "Source directory path";
        var destArg = new Argument<string>("destination");
        destArg.Description = "Destination directory path";

        cmd.Arguments.Add(sourceArg);
        cmd.Arguments.Add(destArg);

        cmd.SetAction(result =>
        {
            var source = result.GetValue(sourceArg) ?? "";
            var dest = result.GetValue(destArg) ?? "";

            var command = new SyncCommand(services);
            return command.ExecuteAsync(source, dest).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private SyncCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(string source, string dest)
    {
        return await ExecuteOperationAsync(
            source, dest, CopyOperationType.Sync,
            false, false, false, null, null, null,
            false, false, false, false, null,
            CancellationToken.None);
    }
}
