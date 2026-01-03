using System.IO.Compression;
using KopioRapido.Models;

namespace KopioRapido.Core;

public class CompressionHelper
{
    /// <summary>
    /// Determines if a file should be compressed based on its extension
    /// </summary>
    public static bool ShouldCompressFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Already compressed formats - don't compress
        HashSet<string> alreadyCompressed = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".gz", ".bz2", ".xz", ".tar",
            ".jpg", ".jpeg", ".png", ".gif", ".webp",
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv",
            ".mp3", ".m4a", ".aac", ".ogg", ".wma", ".flac",
            ".pdf", ".docx", ".xlsx", ".pptx"
        };
        
        if (alreadyCompressed.Contains(extension))
            return false;
        
        // Highly compressible formats - always compress
        HashSet<string> compressible = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".csv", ".json", ".xml", ".sql", 
            ".htm", ".html", ".css", ".js", ".ts",
            ".c", ".cpp", ".h", ".cs", ".java", ".py",
            ".md", ".yaml", ".yml", ".ini", ".conf",
            ".bmp", ".tiff", ".svg"
        };
        
        return compressible.Contains(extension);
    }
    
    /// <summary>
    /// Copies a file with Brotli compression
    /// </summary>
    public static async Task CompressAndCopyAsync(
        string sourceFile,
        string destFile,
        IProgress<CompressionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fileSize = new FileInfo(sourceFile).Length;
        long totalBytesRead = 0;
        long totalBytesWritten = 0;
        
        const int bufferSize = 1024 * 1024; // 1 MB buffer
        var buffer = new byte[bufferSize];
        
        // Create destination directory if needed
        var destDir = Path.GetDirectoryName(destFile);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);
        
        await using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        await using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
        await using var compressionStream = new BrotliStream(destStream, CompressionLevel.Fastest, leaveOpen: false);
        
        int bytesRead;
        while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await compressionStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            
            totalBytesRead += bytesRead;
            totalBytesWritten = destStream.Position; // Compressed size
            
            progress?.Report(new CompressionProgress
            {
                UncompressedBytesProcessed = totalBytesRead,
                CompressedBytesWritten = totalBytesWritten,
                TotalUncompressedSize = fileSize,
                CompressionRatio = totalBytesWritten > 0 ? (double)totalBytesRead / totalBytesWritten : 1.0
            });
        }
        
        // Ensure compression stream is flushed
        await compressionStream.FlushAsync(cancellationToken);
    }
    
    /// <summary>
    /// Copies a compressed file and decompresses it at the destination
    /// </summary>
    public static async Task DecompressAndCopyAsync(
        string sourceFile,
        string destFile,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 1024 * 1024;
        var buffer = new byte[bufferSize];
        long totalBytesWritten = 0;
        
        var destDir = Path.GetDirectoryName(destFile);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);
        
        await using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        await using var decompressionStream = new BrotliStream(sourceStream, CompressionMode.Decompress);
        await using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
        
        int bytesRead;
        while ((bytesRead = await decompressionStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesWritten += bytesRead;
            progress?.Report(totalBytesWritten);
        }
    }
}

public class CompressionProgress
{
    public long UncompressedBytesProcessed { get; set; }
    public long CompressedBytesWritten { get; set; }
    public long TotalUncompressedSize { get; set; }
    public double CompressionRatio { get; set; }
    
    public double PercentComplete => TotalUncompressedSize > 0 
        ? (UncompressedBytesProcessed * 100.0 / TotalUncompressedSize) 
        : 0;
}
