using KopioRapido.Models;

namespace KopioRapido.Services;

public interface IProgressTrackerService
{
    void UpdateFileProgress(string operationId, FileTransferProgress progress);
    void UpdateOverallProgress(string operationId, long bytesTransferred, int filesTransferred);
    void SetTotalSize(string operationId, long totalBytes, int totalFiles);
    void SetProgress(string operationId, long bytesTransferred, int filesTransferred);
    FileTransferProgress? GetCurrentFileProgress(string operationId);
    double GetOverallProgress(string operationId);
    double GetCurrentSpeed(string operationId);
    double GetAverageSpeed(string operationId);
    TimeSpan? GetEstimatedTimeRemaining(string operationId);
}
