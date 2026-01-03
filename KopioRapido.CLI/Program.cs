using System.CommandLine;
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

        // For now, create a simple CLI that just shows help
        // TODO: Complete command implementation
        Console.WriteLine("KopioRapido CLI");
        Console.WriteLine("===============");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  kopiorapido copy <source> <destination>");
        Console.WriteLine("  kopiorapido --version");
        Console.WriteLine();
        Console.WriteLine("Commands will be fully implemented in next phase.");
        Console.WriteLine("Core library and service infrastructure is complete.");
        
        return 0;
    }
}
