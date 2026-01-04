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

        // Global options
        var verboseOption = new Option<bool>("--verbose");
        verboseOption.Description = "Enable verbose output";

        var jsonOption = new Option<bool>("--json");
        jsonOption.Description = "Output results as JSON";

        var plainOption = new Option<bool>("--plain");
        plainOption.Description = "Disable colored output (auto-detected for piped output)";

        var colorOption = new Option<bool>("--color");
        colorOption.Description = "Force colored output even when piped";

        var stateDirOption = new Option<string?>("--state-dir");
        stateDirOption.Description = "Override default state directory for operations/logs";

        var logLevelOption = new Option<string?>("--log-level");
        logLevelOption.Description = "Log level: Debug|Info|Warning|Error";

        rootCommand.Options.Add(verboseOption);
        rootCommand.Options.Add(jsonOption);
        rootCommand.Options.Add(plainOption);
        rootCommand.Options.Add(colorOption);
        rootCommand.Options.Add(stateDirOption);
        rootCommand.Options.Add(logLevelOption);

        // Create global options object for passing to commands
        var globalOptions = new GlobalOptions(verboseOption, jsonOption, plainOption, colorOption, stateDirOption, logLevelOption);
        
        // Add all commands
        rootCommand.Subcommands.Add(CopyCommand.Create(services, globalOptions));
        rootCommand.Subcommands.Add(MoveCommand.Create(services, globalOptions));
        rootCommand.Subcommands.Add(SyncCommand.Create(services, globalOptions));
        rootCommand.Subcommands.Add(MirrorCommand.Create(services, globalOptions));
        rootCommand.Subcommands.Add(BiDirectionalSyncCommand.Create(services, globalOptions));
        rootCommand.Subcommands.Add(ResumeCommand.Create(services, globalOptions));
        rootCommand.Subcommands.Add(ListCommand.Create(services, globalOptions));

        // Parse and execute
        return rootCommand.Parse(args).Invoke();
    }
}
