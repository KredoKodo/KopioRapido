namespace KopioRapido.Models;

/// <summary>
/// Fast scan summary without creating individual FileItem objects
/// </summary>
public class FileScanSummary
{
    public string SourcePath { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public long TotalBytes { get; set; }
    public string FormattedTotalSize { get; set; } = string.Empty;
    
    // Categorization
    public FileCategoryStats Documents { get; set; } = new();
    public FileCategoryStats Images { get; set; } = new();
    public FileCategoryStats Videos { get; set; } = new();
    public FileCategoryStats Audio { get; set; } = new();
    public FileCategoryStats Archives { get; set; } = new();
    public FileCategoryStats Other { get; set; } = new();
    
    // Preview (first 20 files)
    public List<FilePreview> PreviewFiles { get; set; } = new();
    
    // Performance tracking
    public TimeSpan ScanDuration { get; set; }
}

public class FileCategoryStats
{
    public int Count { get; set; }
    public long TotalBytes { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class FilePreview
{
    public string RelativePath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}
