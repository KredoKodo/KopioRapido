namespace KopioRapido.Models;

public class FileTransferProgress
{
    public string FileName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long BytesTransferred { get; set; }
    public double PercentComplete => FileSize > 0 ? (BytesTransferred * 100.0 / FileSize) : 0;
    public double CurrentSpeedBytesPerSecond { get; set; }
    public double AverageSpeedBytesPerSecond { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}
