using Spectre.Console;
using KopioRapido.Models;

namespace KopioRapido.CLI.Output;

public class ConsoleOutputFormatter : IOutputFormatter
{
    private readonly bool _verbose;
    private readonly bool _useRichOutput;

    public ConsoleOutputFormatter(bool verbose, bool plain, bool color)
    {
        _verbose = verbose;
        
        // Auto-detect TTY unless overridden
        if (color)
            _useRichOutput = true;
        else if (plain)
            _useRichOutput = false;
        else
            _useRichOutput = !Console.IsOutputRedirected;
            
        // Configure Spectre.Console
        if (!_useRichOutput)
        {
            AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
        }
    }

    public void ShowAnalysis(StorageProfile source, StorageProfile dest,
                            FileSetProfile files, TransferStrategy strategy)
    {
        if (_useRichOutput)
        {
            AnsiConsole.Write(new Rule("[yellow]📊 Transfer Analysis[/]").RuleStyle("grey").LeftJustified());
            
            var table = new Table();
            table.AddColumn("Property");
            table.AddColumn("Source");
            table.AddColumn("Destination");
            
            table.AddRow("Path", source.Path, dest.Path);
            table.AddRow("Type", source.Type.ToString(), dest.Type.ToString());
            table.AddRow("Speed", $"{source.SequentialWriteMBps:F1} MB/s", $"{dest.SequentialWriteMBps:F1} MB/s");
            table.AddRow("Network", source.IsRemote ? "Yes" : "No", dest.IsRemote ? "Yes" : "No");
            
            AnsiConsole.Write(table);
            
            AnsiConsole.MarkupLine($"\n[cyan]📁 Files:[/] {files.TotalFiles:N0} files, {FormatBytes(files.TotalBytes)}");
            AnsiConsole.MarkupLine($"[cyan]⚡ Strategy:[/] {strategy.UserFriendlyDescription} ({strategy.MaxConcurrentFiles} concurrent)");
            AnsiConsole.MarkupLine($"[cyan]💡 Reasoning:[/] {strategy.Reasoning}");
            
            if (strategy.UseCompression)
            {
                var compressiblePercent = files.TotalFiles > 0 ? (files.CompressibleFiles * 100.0 / files.TotalFiles) : 0;
                AnsiConsole.MarkupLine($"[green]🗜️  Compression enabled[/] ({compressiblePercent:F0}% compressible files)");
            }
        }
        else
        {
            Console.WriteLine("=== Transfer Analysis ===");
            Console.WriteLine($"Source: {source.Path} ({source.Type}, {source.SequentialWriteMBps:F1} MB/s)");
            Console.WriteLine($"Destination: {dest.Path} ({dest.Type}, {dest.SequentialWriteMBps:F1} MB/s)");
            Console.WriteLine($"Files: {files.TotalFiles:N0} files, {FormatBytes(files.TotalBytes)}");
            Console.WriteLine($"Strategy: {strategy.UserFriendlyDescription} ({strategy.MaxConcurrentFiles} concurrent)");
            Console.WriteLine($"Reasoning: {strategy.Reasoning}");
            if (strategy.UseCompression)
            {
                var compressiblePercent = files.TotalFiles > 0 ? (files.CompressibleFiles * 100.0 / files.TotalFiles) : 0;
                Console.WriteLine($"Compression: Enabled ({compressiblePercent:F0}% compressible)");
            }
        }
    }

    public void ShowSyncSummary(SyncOperationSummary summary)
    {
        if (_useRichOutput)
        {
            AnsiConsole.Write(new Rule("[yellow]📋 Dry-Run Summary[/]").RuleStyle("grey").LeftJustified());
            AnsiConsole.MarkupLine($"[cyan]Files to copy:[/] {summary.FilesToCopy}");
            AnsiConsole.MarkupLine($"[cyan]Files to delete:[/] {summary.FilesToDelete}");
            AnsiConsole.MarkupLine($"[cyan]Identical files:[/] {summary.IdenticalFiles}");
            AnsiConsole.MarkupLine($"[cyan]Total size:[/] {FormatBytes(summary.TotalBytesToCopy)}");
        }
        else
        {
            Console.WriteLine("=== Dry-Run Summary ===");
            Console.WriteLine($"Files to copy: {summary.FilesToCopy}");
            Console.WriteLine($"Files to delete: {summary.FilesToDelete}");
            Console.WriteLine($"Identical files: {summary.IdenticalFiles}");
            Console.WriteLine($"Total size: {FormatBytes(summary.TotalBytesToCopy)}");
        }
    }

    public void ShowProgress(FileTransferProgress progress)
    {
        if (_useRichOutput)
        {
            // Spectre.Console progress will be handled by Progress<T> in commands
        }
        else
        {
            var speedMBps = progress.CurrentSpeedBytesPerSecond / (1024.0 * 1024.0);
            Console.WriteLine($"Progress: {progress.PercentComplete:F1}% | {speedMBps:F1} MB/s | {progress.FileName}");
        }
    }

    public void ShowResult(CopyOperation operation)
    {
        var elapsedTime = operation.EndTime.HasValue 
            ? operation.EndTime.Value - operation.StartTime 
            : TimeSpan.Zero;
        var averageSpeedMBps = elapsedTime.TotalSeconds > 0
            ? operation.BytesTransferred / (1024.0 * 1024.0) / elapsedTime.TotalSeconds
            : 0;
        var bytesSaved = operation.TotalUncompressedBytes - operation.TotalCompressedBytes;
        var compressionRatio = operation.TotalCompressedBytes > 0
            ? operation.TotalUncompressedBytes / (double)operation.TotalCompressedBytes
            : 1.0;

        if (_useRichOutput)
        {
            var status = operation.Status switch
            {
                CopyStatus.Completed => "[green]✓ Completed[/]",
                CopyStatus.Failed => "[red]✗ Failed[/]",
                CopyStatus.Cancelled => "[yellow]⊘ Cancelled[/]",
                _ => "[grey]○ Unknown[/]"
            };
            
            AnsiConsole.Write(new Rule($"[bold]{status}[/]").RuleStyle("grey").LeftJustified());
            
            AnsiConsole.MarkupLine($"[cyan]Operation ID:[/] {operation.Id}");
            AnsiConsole.MarkupLine($"[cyan]Type:[/] {operation.OperationType}");
            AnsiConsole.MarkupLine($"[cyan]Files transferred:[/] {operation.FilesTransferred:N0}");
            AnsiConsole.MarkupLine($"[cyan]Bytes transferred:[/] {FormatBytes(operation.BytesTransferred)}");
            
            if (bytesSaved > 0)
            {
                AnsiConsole.MarkupLine($"[green]🗜️  Compression:[/] {operation.FilesCompressed} files, {FormatBytes(bytesSaved)} saved ({compressionRatio:F1}x ratio)");
            }
            
            AnsiConsole.MarkupLine($"[cyan]Time:[/] {elapsedTime:hh\\:mm\\:ss}");
            AnsiConsole.MarkupLine($"[cyan]Average speed:[/] {averageSpeedMBps:F1} MB/s");
            
            if (!string.IsNullOrEmpty(operation.ErrorMessage))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {operation.ErrorMessage}");
            }
        }
        else
        {
            Console.WriteLine("=== Operation Complete ===");
            Console.WriteLine($"Status: {operation.Status}");
            Console.WriteLine($"Operation ID: {operation.Id}");
            Console.WriteLine($"Type: {operation.OperationType}");
            Console.WriteLine($"Files transferred: {operation.FilesTransferred:N0}");
            Console.WriteLine($"Bytes transferred: {FormatBytes(operation.BytesTransferred)}");
            
            if (bytesSaved > 0)
            {
                Console.WriteLine($"Compression: {operation.FilesCompressed} files, {FormatBytes(bytesSaved)} saved ({compressionRatio:F1}x ratio)");
            }
            
            Console.WriteLine($"Time: {elapsedTime:hh\\:mm\\:ss}");
            Console.WriteLine($"Average speed: {averageSpeedMBps:F1} MB/s");
            
            if (!string.IsNullOrEmpty(operation.ErrorMessage))
            {
                Console.WriteLine($"Error: {operation.ErrorMessage}");
            }
        }
    }

    public void Error(string message)
    {
        if (_useRichOutput)
            AnsiConsole.MarkupLine($"[red]✗ Error:[/] {message}");
        else
            Console.Error.WriteLine($"ERROR: {message}");
    }

    public void Warning(string message)
    {
        if (_useRichOutput)
            AnsiConsole.MarkupLine($"[yellow]⚠ Warning:[/] {message}");
        else
            Console.WriteLine($"WARNING: {message}");
    }

    public void Info(string message)
    {
        if (_verbose)
        {
            if (_useRichOutput)
                AnsiConsole.MarkupLine($"[grey]ℹ {message}[/]");
            else
                Console.WriteLine($"INFO: {message}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
