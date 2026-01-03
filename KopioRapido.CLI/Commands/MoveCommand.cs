using System.CommandLine;
using KopioRapido.Models;

namespace KopioRapido.CLI.Commands;

public class MoveCommand : BaseCommand
{
    public static Command Create(IServiceProvider services)
    {
        var cmd = new Command("move", "Move files from source to destination (copy then delete)");

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

            var command = new MoveCommand(services);
            return command.ExecuteAsync(source, dest).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private MoveCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(string source, string dest)
    {
        return await ExecuteOperationAsync(
            source, dest, CopyOperationType.Move,
            false, false, false, null, null, null,
            false, false, false, false, null,
            CancellationToken.None);
    }
}
