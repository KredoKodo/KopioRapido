using System.CommandLine;
using KopioRapido.Models;

namespace KopioRapido.CLI.Commands;

public class MirrorCommand : BaseCommand
{
    public static Command Create(IServiceProvider services)
    {
        var cmd = new Command("mirror", "Mirror source to destination (includes deletions)");

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

            var command = new MirrorCommand(services);
            return command.ExecuteAsync(source, dest).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private MirrorCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(string source, string dest)
    {
        return await ExecuteOperationAsync(
            source, dest, CopyOperationType.Mirror,
            false, false, false, null, null, null,
            false, false, false, false, null,
            CancellationToken.None);
    }
}
