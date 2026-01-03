using System.CommandLine;
using KopioRapido.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KopioRapido.CLI.Commands;

public class ResumeCommand : BaseCommand
{
    public static Command Create(IServiceProvider services)
    {
        var cmd = new Command("resume", "Resume an interrupted operation");

        var operationIdArg = new Argument<string>("operation-id");
        operationIdArg.Description = "Operation ID to resume (GUID)";

        cmd.Arguments.Add(operationIdArg);

        cmd.SetAction(result =>
        {
            var operationId = result.GetValue(operationIdArg) ?? "";

            var command = new ResumeCommand(services);
            return command.ExecuteAsync(operationId).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private ResumeCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(string operationId)
    {
        try
        {
            var resumeService = Services.GetRequiredService<IResumeService>();
            var operation = await resumeService.LoadOperationStateAsync(operationId);
            
            if (operation == null)
            {
                Console.WriteLine($"ERROR: Operation {operationId} not found");
                return 1;
            }

            Console.WriteLine($"Resuming {operation.OperationType} operation:");
            Console.WriteLine($"  Source: {operation.SourcePath}");
            Console.WriteLine($"  Destination: {operation.DestinationPath}");
            Console.WriteLine($"  Progress: {operation.FilesTransferred}/{operation.TotalFiles} files");
            Console.WriteLine();

            // TODO: Actually resume the operation
            // This requires integration with FileOperationService.ResumeOperationAsync()
            Console.WriteLine("Resume functionality will be completed in next iteration.");
            Console.WriteLine("Core resume infrastructure is in place.");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }
}
