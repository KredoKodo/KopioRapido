using System.CommandLine;
using KopioRapido.Services;
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

            var command = new ResumeCommand(services);
            return command.ExecuteAsync(operationId, verbose, json).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private ResumeCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(string operationId, bool verbose, bool json)
    {
        try
        {
            var resumeService = Services.GetRequiredService<IResumeService>();
            var operation = await resumeService.LoadOperationStateAsync(operationId);
            
            if (operation == null)
            {
                if (json)
                {
                    Console.WriteLine($"{{\"error\": \"Operation {operationId} not found\"}}");
                }
                else
                {
                    Console.WriteLine($"ERROR: Operation {operationId} not found");
                }
                return 1;
            }

            if (json)
            {
                var jsonOutput = System.Text.Json.JsonSerializer.Serialize(new
                {
                    operationType = operation.OperationType.ToString(),
                    sourcePath = operation.SourcePath,
                    destinationPath = operation.DestinationPath,
                    filesTransferred = operation.FilesTransferred,
                    totalFiles = operation.TotalFiles,
                    status = "Not yet implemented"
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(jsonOutput);
            }
            else
            {
                Console.WriteLine($"Resuming {operation.OperationType} operation:");
                Console.WriteLine($"  Source: {operation.SourcePath}");
                Console.WriteLine($"  Destination: {operation.DestinationPath}");
                Console.WriteLine($"  Progress: {operation.FilesTransferred}/{operation.TotalFiles} files");
                Console.WriteLine();
                Console.WriteLine("Resume functionality will be completed in next iteration.");
                Console.WriteLine("Core resume infrastructure is in place.");
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            if (json)
            {
                Console.WriteLine($"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\"}}");
            }
            else
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
            return 1;
        }
    }
}
