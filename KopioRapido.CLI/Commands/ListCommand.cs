using System.CommandLine;
using KopioRapido.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KopioRapido.CLI.Commands;

public class ListCommand : BaseCommand
{
    public static Command Create(IServiceProvider services, GlobalOptions globalOptions)
    {
        var cmd = new Command("list", "List resumable operations");

        cmd.SetAction(result =>
        {
            // Get global options
            var json = result.GetValue(globalOptions.Json);
            var stateDir = result.GetValue(globalOptions.StateDir);
            
            var command = new ListCommand(services);
            return command.ExecuteAsync(json, stateDir).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private ListCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(bool json, string? stateDir)
    {
        try
        {
            // Configure custom state directory if specified
            ConfigureStateDirectory(stateDir);
            
            var resumeService = Services.GetRequiredService<IResumeService>();
            var resumableOps = await resumeService.GetResumableOperationsAsync();
            var operations = resumableOps.ToList();
            
            if (operations.Count == 0)
            {
                if (json)
                {
                    Console.WriteLine("{\"operations\": []}");
                }
                else
                {
                    Console.WriteLine("No resumable operations found.");
                }
                return 0;
            }

            if (json)
            {
                var jsonOperations = operations.Select(operation => new
                {
                    id = operation.Id,
                    operationType = operation.OperationType.ToString(),
                    status = operation.Status.ToString(),
                    sourcePath = operation.SourcePath,
                    destinationPath = operation.DestinationPath,
                    filesTransferred = operation.FilesTransferred,
                    totalFiles = operation.TotalFiles,
                    startTime = operation.StartTime
                }).ToList();
                
                var jsonOutput = System.Text.Json.JsonSerializer.Serialize(
                    new { operations = jsonOperations },
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                );
                Console.WriteLine(jsonOutput);
            }
            else
            {
                foreach (var operation in operations)
                {
                    Console.WriteLine($"Operation ID: {operation.Id}");
                    Console.WriteLine($"  Type: {operation.OperationType}");
                    Console.WriteLine($"  Status: {operation.Status}");
                    Console.WriteLine($"  Source: {operation.SourcePath}");
                    Console.WriteLine($"  Destination: {operation.DestinationPath}");
                    Console.WriteLine($"  Progress: {operation.FilesTransferred}/{operation.TotalFiles} files");
                    Console.WriteLine($"  Started: {operation.StartTime:g}");
                    Console.WriteLine();
                }
                Console.WriteLine($"Total: {operations.Count} resumable operation(s)");
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
