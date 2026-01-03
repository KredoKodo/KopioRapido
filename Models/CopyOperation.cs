namespace KopioRapido.Models;

public class CopyOperation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public CopyOperationType OperationType { get; set; }
    public CopyStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long TotalBytes { get; set; }
    public long BytesTransferred { get; set; }
    public int TotalFiles { get; set; }
    public int FilesTransferred { get; set; }
    public string? CurrentFile { get; set; }
    public string? ErrorMessage { get; set; }
    public bool CanResume { get; set; }
    public List<CompletedFileInfo> CompletedFiles { get; set; } = new();
    
    // Operation-specific stats
    public int FilesDeleted { get; set; }
    public int FilesSkipped { get; set; }
    
    // Compression stats
    public long TotalCompressedBytes { get; set; }
    public long TotalUncompressedBytes { get; set; }
    public int FilesCompressed { get; set; }
}

public class CompletedFileInfo
{
    public string RelativePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime CompletedAt { get; set; }
}

public enum CopyOperationType
{
    Copy,
    Move,
    Sync,
    Mirror,
    BiDirectionalSync
}

public enum CopyStatus
{
    Pending,
    InProgress,
    Paused,
    Completed,
    Failed,
    Cancelled
}
