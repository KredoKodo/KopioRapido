using System.Collections.Concurrent;
using KopioRapido.Models;

namespace KopioRapido.Services;

public class ProgressTrackerService : IProgressTrackerService
{
    private readonly ConcurrentDictionary<string, OperationProgress> _operations;

    public ProgressTrackerService()
    {
        _operations = new ConcurrentDictionary<string, OperationProgress>();
    }

    public void UpdateFileProgress(string operationId, FileTransferProgress progress)
    {
        var operation = _operations.GetOrAdd(operationId, _ => new OperationProgress());
        operation.CurrentFile = progress;
        operation.LastUpdateTime = DateTime.UtcNow;
    }

    public void UpdateOverallProgress(string operationId, long bytesTransferred, int filesTransferred)
    {
        var operation = _operations.GetOrAdd(operationId, _ => new OperationProgress());
        operation.TotalBytesTransferred = bytesTransferred;
        operation.TotalFilesTransferred = filesTransferred;
        operation.LastUpdateTime = DateTime.UtcNow;

        if (operation.StartTime == DateTime.MinValue)
        {
            operation.StartTime = DateTime.UtcNow;
        }
    }

    public void SetTotalSize(string operationId, long totalBytes, int totalFiles)
    {
        var operation = _operations.GetOrAdd(operationId, _ => new OperationProgress());
        operation.TotalBytesExpected = totalBytes;
        operation.TotalFilesExpected = totalFiles;
    }

    public void SetProgress(string operationId, long bytesTransferred, int filesTransferred)
    {
        var operation = _operations.GetOrAdd(operationId, _ => new OperationProgress());
        operation.TotalBytesTransferred = bytesTransferred;
        operation.TotalFilesTransferred = filesTransferred;
        operation.StartTime = DateTime.UtcNow; // Reset start time for accurate speed calculation
        operation.LastUpdateTime = DateTime.UtcNow;
    }

    public FileTransferProgress? GetCurrentFileProgress(string operationId)
    {
        return _operations.TryGetValue(operationId, out var operation) ? operation.CurrentFile : null;
    }

    public double GetOverallProgress(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
        {
            return 0;
        }

        if (operation.TotalBytesExpected == 0)
        {
            return 0;
        }

        return (operation.TotalBytesTransferred * 100.0) / operation.TotalBytesExpected;
    }

    public double GetCurrentSpeed(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
        {
            return 0;
        }

        var currentFile = operation.CurrentFile;
        if (currentFile == null)
        {
            return 0;
        }

        return currentFile.CurrentSpeedBytesPerSecond;
    }

    public double GetAverageSpeed(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
        {
            return 0;
        }

        if (operation.StartTime == DateTime.MinValue)
        {
            return 0;
        }

        var elapsedSeconds = (DateTime.UtcNow - operation.StartTime).TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return 0;
        }

        return operation.TotalBytesTransferred / elapsedSeconds;
    }

    public TimeSpan? GetEstimatedTimeRemaining(string operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
        {
            return null;
        }

        var avgSpeed = GetAverageSpeed(operationId);
        if (avgSpeed <= 0 || operation.TotalBytesExpected == 0)
        {
            return null;
        }

        var bytesRemaining = operation.TotalBytesExpected - operation.TotalBytesTransferred;
        if (bytesRemaining <= 0)
        {
            return TimeSpan.Zero;
        }

        var secondsRemaining = bytesRemaining / avgSpeed;
        return TimeSpan.FromSeconds(secondsRemaining);
    }

    private class OperationProgress
    {
        public DateTime StartTime { get; set; } = DateTime.MinValue;
        public DateTime LastUpdateTime { get; set; } = DateTime.MinValue;
        public long TotalBytesExpected { get; set; }
        public int TotalFilesExpected { get; set; }
        public long TotalBytesTransferred { get; set; }
        public int TotalFilesTransferred { get; set; }
        public FileTransferProgress? CurrentFile { get; set; }
    }
}
