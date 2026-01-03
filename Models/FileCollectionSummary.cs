namespace KopioRapido.Models;

public class FileCollectionSummary
{
    public int TotalFiles { get; set; }
    public int TotalDirectories { get; set; }
    public long TotalSize { get; set; }
    public int PreviewFileCount { get; set; }
    public List<FileItem> PreviewFiles { get; set; } = new();
    
    public string FormattedTotalSize => FormatSize(TotalSize);
    
    public string SummaryText => 
        $"{TotalFiles:N0} files in {TotalDirectories:N0} folders ({FormattedTotalSize})";
    
    private static string FormatSize(long bytes)
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
