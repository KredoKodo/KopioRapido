using System.CommandLine;
using KopioRapido.Models;

namespace KopioRapido.CLI.Commands;

public class BiDirectionalSyncCommand : BaseCommand
{
    public static Command Create(IServiceProvider services)
    {
        var cmd = new Command("bidirectional-sync", "Two-way sync (newer timestamp wins)");

        var path1Arg = new Argument<string>("path1");
        path1Arg.Description = "First directory path";
        var path2Arg = new Argument<string>("path2");
        path2Arg.Description = "Second directory path";

        cmd.Arguments.Add(path1Arg);
        cmd.Arguments.Add(path2Arg);

        cmd.SetAction(result =>
        {
            var path1 = result.GetValue(path1Arg) ?? "";
            var path2 = result.GetValue(path2Arg) ?? "";

            var command = new BiDirectionalSyncCommand(services);
            return command.ExecuteAsync(path1, path2).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private BiDirectionalSyncCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(string path1, string path2)
    {
        return await ExecuteOperationAsync(
            path1, path2, CopyOperationType.BiDirectionalSync,
            false, false, false, null, null, null,
            false, false, false, false, null,
            CancellationToken.None);
    }
}
