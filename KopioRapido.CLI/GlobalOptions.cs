using System.CommandLine;

namespace KopioRapido.CLI;

/// <summary>
/// Holds references to global options for access by subcommands.
/// System.CommandLine 2.0.1 doesn't provide an easy way to access parent command options,
/// so we pass this object to command factories to enable global option access.
/// </summary>
public class GlobalOptions
{
    public Option<bool> Verbose { get; }
    public Option<bool> Json { get; }
    public Option<bool> Plain { get; }
    public Option<bool> Color { get; }
    public Option<string?> StateDir { get; }
    public Option<string?> LogLevel { get; }

    public GlobalOptions(
        Option<bool> verbose,
        Option<bool> json,
        Option<bool> plain,
        Option<bool> color,
        Option<string?> stateDir,
        Option<string?> logLevel)
    {
        Verbose = verbose;
        Json = json;
        Plain = plain;
        Color = color;
        StateDir = stateDir;
        LogLevel = logLevel;
    }
}
