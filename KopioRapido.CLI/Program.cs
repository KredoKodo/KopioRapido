using System.CommandLine;
using KopioRapido.CLI.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace KopioRapido.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Handle --version flag
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            Console.WriteLine($"KopioRapido CLI v1.0.0 (.NET {Environment.Version})");
            return 0;
        }

        // Configure services (DI)
        var services = ServiceConfiguration.ConfigureServices();

        // Create root command
        var rootCommand = new RootCommand("KopioRapido - High-performance file copy tool");

        // Add all commands
        rootCommand.Subcommands.Add(CopyCommand.Create(services));
        rootCommand.Subcommands.Add(MoveCommand.Create(services));
        rootCommand.Subcommands.Add(SyncCommand.Create(services));
        rootCommand.Subcommands.Add(MirrorCommand.Create(services));
        rootCommand.Subcommands.Add(BiDirectionalSyncCommand.Create(services));
        rootCommand.Subcommands.Add(ResumeCommand.Create(services));
        rootCommand.Subcommands.Add(ListCommand.Create(services));

        // Parse and execute
        return rootCommand.Parse(args).Invoke();
    }
}
