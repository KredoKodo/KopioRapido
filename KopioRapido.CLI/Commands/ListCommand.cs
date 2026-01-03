using System.CommandLine;
using KopioRapido.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KopioRapido.CLI.Commands;

public class ListCommand : BaseCommand
{
    public static Command Create(IServiceProvider services)
    {
        var cmd = new Command("list", "List resumable operations");

        cmd.SetAction(result =>
        {
            var command = new ListCommand(services);
            return command.ExecuteAsync().GetAwaiter().GetResult();
        });

        return cmd;
    }

    private ListCommand(IServiceProvider services) : base(services) { }

    private async Task<int> ExecuteAsync()
    {
        try
        {
            var resumeService = Services.GetRequiredService<IResumeService>();
            
            // Get app data directory
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var operationsDir = Path.Combine(appData, "KopioRapido", "Operations");
            
            if (!Directory.Exists(operationsDir))
            {
                Console.WriteLine("No operations found.");
                return 0;
            }

            var stateFiles = Directory.GetFiles(operationsDir, "*.json");
            
            if (stateFiles.Length == 0)
            {
                Console.WriteLine("No resumable operations found.");
                return 0;
            }

            Console.WriteLine($"Found {stateFiles.Length} resumable operation(s):");
            Console.WriteLine();

            foreach (var file in stateFiles)
            {
                var operationId = Path.GetFileNameWithoutExtension(file);
                var operation = await resumeService.LoadOperationStateAsync(operationId);
                
                if (operation != null)
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

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }
}
