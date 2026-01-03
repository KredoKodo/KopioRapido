using System.Diagnostics;
using KopioRapido.Models;

namespace KopioRapido.Services;

public class QuickScanService : IQuickScanService
{
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".doc", ".docx", ".pdf", ".xls", ".xlsx", ".ppt", ".pptx",
        ".odt", ".ods", ".odp", ".rtf", ".csv", ".xml", ".json", ".md"
    };
    
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".ico", ".webp",
        ".tiff", ".tif", ".heic", ".heif", ".raw", ".cr2", ".nef"
    };
    
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v",
        ".mpg", ".mpeg", ".3gp", ".ogv", ".ts", ".mts", ".m2ts"
    };
    
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus",
        ".ape", ".alac", ".aiff", ".dsd", ".dsf"
    };
    
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso",
        ".dmg", ".pkg", ".deb", ".rpm"
    };

    public async Task<FileScanSummary> QuickScanAsync(string sourcePath, int previewCount = 20, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var summary = new FileScanSummary
        {
            SourcePath = sourcePath,
            Documents = new FileCategoryStats { Icon = "📄", Label = "documents" },
            Images = new FileCategoryStats { Icon = "🖼️", Label = "images" },
            Videos = new FileCategoryStats { Icon = "🎬", Label = "videos" },
            Audio = new FileCategoryStats { Icon = "🎵", Label = "audio" },
            Archives = new FileCategoryStats { Icon = "📦", Label = "archives" },
            Other = new FileCategoryStats { Icon = "📋", Label = "other" }
        };

        await Task.Run(() =>
        {
            try
            {
                // Fast enumeration - don't create FileInfo objects unless needed
                var enumerator = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories);
                
                foreach (var filePath in enumerator)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var extension = fileInfo.Extension.ToLowerInvariant();
                        var fileSize = fileInfo.Length;
                        
                        summary.TotalFiles++;
                        summary.TotalBytes += fileSize;
                        
                        // Categorize
                        if (DocumentExtensions.Contains(extension))
                        {
                            summary.Documents.Count++;
                            summary.Documents.TotalBytes += fileSize;
                        }
                        else if (ImageExtensions.Contains(extension))
                        {
                            summary.Images.Count++;
                            summary.Images.TotalBytes += fileSize;
                        }
                        else if (VideoExtensions.Contains(extension))
                        {
                            summary.Videos.Count++;
                            summary.Videos.TotalBytes += fileSize;
                        }
                        else if (AudioExtensions.Contains(extension))
                        {
                            summary.Audio.Count++;
                            summary.Audio.TotalBytes += fileSize;
                        }
                        else if (ArchiveExtensions.Contains(extension))
                        {
                            summary.Archives.Count++;
                            summary.Archives.TotalBytes += fileSize;
                        }
                        else
                        {
                            summary.Other.Count++;
                            summary.Other.TotalBytes += fileSize;
                        }
                        
                        // Add to preview (first N files only)
                        if (summary.PreviewFiles.Count < previewCount)
                        {
                            var relativePath = Path.GetRelativePath(sourcePath, filePath);
                            summary.PreviewFiles.Add(new FilePreview
                            {
                                RelativePath = relativePath,
                                FullPath = filePath,
                                SizeBytes = fileSize,
                                FormattedSize = FormatBytes(fileSize),
                                Icon = GetIconForExtension(extension)
                            });
                        }
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }
                
                // Format sizes
                summary.FormattedTotalSize = FormatBytes(summary.TotalBytes);
                summary.Documents.FormattedSize = FormatBytes(summary.Documents.TotalBytes);
                summary.Images.FormattedSize = FormatBytes(summary.Images.TotalBytes);
                summary.Videos.FormattedSize = FormatBytes(summary.Videos.TotalBytes);
                summary.Audio.FormattedSize = FormatBytes(summary.Audio.TotalBytes);
                summary.Archives.FormattedSize = FormatBytes(summary.Archives.TotalBytes);
                summary.Other.FormattedSize = FormatBytes(summary.Other.TotalBytes);
            }
            catch
            {
                // Handle directory access errors
            }
        }, cancellationToken);
        
        stopwatch.Stop();
        summary.ScanDuration = stopwatch.Elapsed;
        
        return summary;
    }
    
    private string GetIconForExtension(string extension)
    {
        if (DocumentExtensions.Contains(extension)) return "📄";
        if (ImageExtensions.Contains(extension)) return "🖼️";
        if (VideoExtensions.Contains(extension)) return "🎬";
        if (AudioExtensions.Contains(extension)) return "🎵";
        if (ArchiveExtensions.Contains(extension)) return "📦";
        return "📋";
    }
    
    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
