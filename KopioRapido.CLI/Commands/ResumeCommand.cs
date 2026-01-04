using System.CommandLine;
using KopioRapido.Services;
using KopioRapido.Models;
using KopioRapido.CLI.Output;
using Microsoft.Extensions.DependencyInjection;

namespace KopioRapido.CLI.Commands;

public class ResumeCommand : BaseCommand
{
    public static Command Create(IServiceProvider services, GlobalOptions globalOptions)
    {
        var cmd = new Command("resume", "Resume an interrupted operation");

        var operationIdArg = new Argument<string>("operation-id");
        operationIdArg.Description = "Operation ID to resume (GUID)";

        cmd.Arguments.Add(operationIdArg);

        cmd.SetAction(result =>
        {
            var operationId = result.GetValue(operationIdArg) ?? "";
            
            // Get global options
            var verbose = result.GetValue(globalOptions.Verbose);
            var json = result.GetValue(globalOptions.Json);
            var plain = result.GetValue(globalOptions.Plain);
            var color = result.GetValue(globalOptions.Color);

            var command = new ResumeCommand(services);
            return command.ExecuteAsync(operationId, verbose, json, plain, color).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private ResumeCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(string operationId, bool verbose, bool json, bool plain, bool color)
    {
        var outputFormatter = json ?
            (IOutputFormatter)new JsonOutputFormatter() :
            new ConsoleOutputFormatter(verbose, plain, color);

        try
        {
            var resumeService = Services.GetRequiredService<IResumeService>();
            var operation = await resumeService.LoadOperationStateAsync(operationId);
            
            if (operation == null)
            {
                outputFormatter.Error($"Operation {operationId} not found");
                return 1;
            }

            if (operation.Status == CopyStatus.Completed)
            {
                outputFormatter.Warning($"Operation {operationId} is already completed");
                outputFormatter.ShowResult(operation);
                return 0;
            }

            if (!json)
            {
                outputFormatter.Info($"Resuming {operation.OperationType} operation:");
                outputFormatter.Info($"  Source: {operation.SourcePath}");
                outputFormatter.Info($"  Destination: {operation.DestinationPath}");
                outputFormatter.Info($"  Progress: {operation.FilesTransferred}/{operation.TotalFiles} files");
                outputFormatter.Info($"  Status: {operation.Status}");
                Console.WriteLine();
            }

            // Execute resume with progress reporting
            var progress = new Progress<FileTransferProgress>(p =>
                outputFormatter.ShowProgress(p));

            var resumedOperation = await FileOpService.ResumeCopyAsync(
                operationId,
                progress,
                CancellationToken.None);

            outputFormatter.ShowResult(resumedOperation);

            return resumedOperation.Status == CopyStatus.Completed ? 0 : 1;
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
}
