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
            
            var command = new ListCommand(services);
            return command.ExecuteAsync(json).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private ListCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync(bool json)
    {
        try
        {
            var resumeService = Services.GetRequiredService<IResumeService>();
            
            // Get app data directory
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var operationsDir = Path.Combine(appData, "KopioRapido", "Operations");
            
            if (!Directory.Exists(operationsDir))
            {
                if (json)
                {
                    Console.WriteLine("{\"operations\": []}");
                }
                else
                {
                    Console.WriteLine("No operations found.");
                }
                return 0;
            }

            var stateFiles = Directory.GetFiles(operationsDir, "*.json");
            
            if (stateFiles.Length == 0)
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

            var operations = new List<object>();
            
            foreach (var file in stateFiles)
            {
                var operationId = Path.GetFileNameWithoutExtension(file);
                var operation = await resumeService.LoadOperationStateAsync(operationId);
                
                if (operation != null)
                {
                    if (json)
                    {
                        operations.Add(new
                        {
                            id = operation.Id,
                            operationType = operation.OperationType.ToString(),
                            status = operation.Status.ToString(),
                            sourcePath = operation.SourcePath,
                            destinationPath = operation.DestinationPath,
                            filesTransferred = operation.FilesTransferred,
                            totalFiles = operation.TotalFiles,
                            startTime = operation.StartTime
                        });
                    }
                    else
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
                }
            }

            if (json)
            {
                var jsonOutput = System.Text.Json.JsonSerializer.Serialize(
                    new { operations },
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                );
                Console.WriteLine(jsonOutput);
            }
            else
            {
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
