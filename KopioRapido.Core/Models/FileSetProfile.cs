namespace KopioRapido.Models;

public class FileSetProfile
{
    public int TotalFiles { get; set; }
    public long TotalBytes { get; set; }
    public int TinyFiles { get; set; }        // < 1MB
    public int SmallFiles { get; set; }       // 1-10MB
    public int MediumFiles { get; set; }      // 10-100MB
    public int LargeFiles { get; set; }       // 100MB-1GB
    public int HugeFiles { get; set; }        // > 1GB
    public double AverageFileSizeMB { get; set; }
    public int MaxDirectoryDepth { get; set; }
    public int CompressibleFiles { get; set; }
    public int AlreadyCompressedFiles { get; set; }
    public Dictionary<string, int> FileTypeDistribution { get; set; } = new();
    
    public string Summary => TotalFiles switch
    {
        < 10 => $"{TotalFiles} files ({FormatBytes(TotalBytes)})",
        < 100 => $"{TotalFiles} files, mostly {DominantSizeCategory}",
        < 1000 => $"{TotalFiles} files, {DominantSizeCategory}",
        _ => $"{TotalFiles:N0} files, {DominantSizeCategory}"
    };
    
    private string DominantSizeCategory
    {
        get
        {
            if (HugeFiles > TotalFiles / 2) return "large files";
            if (LargeFiles > TotalFiles / 2) return "medium-large files";
            if (MediumFiles > TotalFiles / 2) return "medium files";
            if (SmallFiles > TotalFiles / 2) return "small files";
            return "tiny files";
        }
    }
    
    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return $"{size:F1} {suffixes[suffixIndex]}";
    }
}
