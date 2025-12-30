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
