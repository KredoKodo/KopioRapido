namespace KopioRapido.Models;

public class VerificationResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public int TotalFilesCompared { get; set; }
    public int IdenticalFiles { get; set; }
    public List<FileComparisonInfo> DifferentFiles { get; set; } = new();
    public List<FileComparisonInfo> MissingFiles { get; set; } = new();
    public List<FileComparisonInfo> ExtraFiles { get; set; } = new();

    public long TotalBytesDifferent { get; set; }

    public bool IsIdentical => DifferentFiles.Count == 0 &&
                               MissingFiles.Count == 0 &&
                               ExtraFiles.Count == 0;
}

public class FileComparisonInfo
{
    public string RelativePath { get; set; } = string.Empty;
    public long? SourceSize { get; set; }
    public DateTime? SourceLastModified { get; set; }
    public long? DestinationSize { get; set; }
    public DateTime? DestinationLastModified { get; set; }
    public string Reason { get; set; } = string.Empty;
}
