using System.Collections.Concurrent;
using System.Diagnostics;
using FastRsync.Core;
using FastRsync.Delta;
using FastRsync.Diagnostics;
using FastRsync.Signature;
using KopioRapido.Models;
using KopioRapido.Services;

namespace KopioRapido.Core;

public class FileCopyEngine
{
    private readonly ILoggingService _loggingService;
    private readonly IProgressTrackerService _progressTracker;
    private readonly IResumeService _resumeService;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _operations;
    private const long DELTA_THRESHOLD_BYTES = 10 * 1024 * 1024; // 10 MB

    public FileCopyEngine(
        ILoggingService loggingService,
        IProgressTrackerService progressTracker,
        IResumeService resumeService)
    {
        _loggingService = loggingService;
        _progressTracker = progressTracker;
        _resumeService = resumeService;
        _operations = new ConcurrentDictionary<string, CancellationTokenSource>();
    }

    public async Task<CopyOperation> CopyAsync(
        string sourcePath,
        string destinationPath,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = new CopyOperation
        {
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            OperationType = CopyOperationType.Copy,
            Status = CopyStatus.InProgress,
            StartTime = DateTime.UtcNow,
            CanResume = true
        };

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _operations.TryAdd(operation.Id, cts);

        try
        {
            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                $"Starting copy operation from '{sourcePath}' to '{destinationPath}'");

            await AnalyzeSourceAsync(operation, sourcePath);
            _progressTracker.SetTotalSize(operation.Id, operation.TotalBytes, operation.TotalFiles);
            await _resumeService.SaveOperationStateAsync(operation);

            if (File.Exists(sourcePath))
            {
                await CopyFileAsync(operation, sourcePath, destinationPath, progress, cts.Token);
            }
            else if (Directory.Exists(sourcePath))
            {
                await CopyDirectoryAsync(operation, sourcePath, destinationPath, progress, cts.Token);
            }
            else
            {
                throw new FileNotFoundException($"Source path not found: {sourcePath}");
            }

            operation.Status = CopyStatus.Completed;
            operation.EndTime = DateTime.UtcNow;

            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                $"Copy operation completed successfully. {operation.FilesTransferred} files, {FormatBytes(operation.BytesTransferred)} transferred");
        }
        catch (OperationCanceledException)
        {
            operation.Status = CopyStatus.Cancelled;
            operation.EndTime = DateTime.UtcNow;
            await _loggingService.LogAsync(operation.Id, LogLevel.Warning, "Copy operation was cancelled");
        }
        catch (Exception ex)
        {
            operation.Status = CopyStatus.Failed;
            operation.EndTime = DateTime.UtcNow;
            operation.ErrorMessage = ex.Message;
            await _loggingService.LogAsync(operation.Id, LogLevel.Error,
                $"Copy operation failed: {ex.Message}", exception: ex);
        }
        finally
        {
            await _resumeService.SaveOperationStateAsync(operation);
            _operations.TryRemove(operation.Id, out _);
        }

        return operation;
    }

    private async Task AnalyzeSourceAsync(CopyOperation operation, string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            var fileInfo = new FileInfo(sourcePath);
            operation.TotalBytes = fileInfo.Length;
            operation.TotalFiles = 1;
        }
        else if (Directory.Exists(sourcePath))
        {
            var (totalBytes, totalFiles) = await Task.Run(() => CalculateDirectorySize(sourcePath));
            operation.TotalBytes = totalBytes;
            operation.TotalFiles = totalFiles;
        }
    }

    private (long totalBytes, int totalFiles) CalculateDirectorySize(string directoryPath)
    {
        long totalBytes = 0;
        int totalFiles = 0;

        try
        {
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            totalFiles = files.Length;

            foreach (var file in files)
            {
                try
                {
                    totalBytes += new FileInfo(file).Length;
                }
                catch
                {
                    // Skip files we can't access
                }
            }
        }
        catch
        {
            // Return what we have so far
        }

        return (totalBytes, totalFiles);
    }

    private async Task CopyFileAsync(
        CopyOperation operation,
        string sourceFile,
        string destFile,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(sourceFile);
        var fileName = Path.GetFileName(sourceFile);

        operation.CurrentFile = fileName;
        await _resumeService.SaveOperationStateAsync(operation);

        await _loggingService.LogAsync(operation.Id, LogLevel.Info,
            $"Copying file: {fileName} ({FormatBytes(fileInfo.Length)})", sourceFile);

        var destDir = Path.GetDirectoryName(destFile);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        var stopwatch = Stopwatch.StartNew();

        bool useDeltaSync = ShouldUseDeltaSync(sourceFile, destFile);

        if (useDeltaSync)
        {
            await CopyFileWithDeltaSyncAsync(operation, sourceFile, destFile, fileInfo.Length, progress, cancellationToken);
        }
        else
        {
            await CopyFileDirectAsync(operation, sourceFile, destFile, fileInfo.Length, progress, cancellationToken);
        }

        stopwatch.Stop();

        operation.FilesTransferred++;
        operation.BytesTransferred += fileInfo.Length;
        _progressTracker.UpdateOverallProgress(operation.Id, operation.BytesTransferred, operation.FilesTransferred);
        operation.CurrentFile = null;

        await _loggingService.LogAsync(operation.Id, LogLevel.Info,
            $"File copied successfully: {fileName} in {stopwatch.Elapsed.TotalSeconds:F2}s", sourceFile);
    }

    private bool ShouldUseDeltaSync(string sourceFile, string destFile)
    {
        if (!File.Exists(destFile))
        {
            return false;
        }

        var sourceInfo = new FileInfo(sourceFile);
        if (sourceInfo.Length < DELTA_THRESHOLD_BYTES)
        {
            return false;
        }

        var destInfo = new FileInfo(destFile);
        if (sourceInfo.Length == destInfo.Length &&
            sourceInfo.LastWriteTimeUtc == destInfo.LastWriteTimeUtc)
        {
            return false;
        }

        return true;
    }

    private async Task CopyFileWithDeltaSyncAsync(
        CopyOperation operation,
        string sourceFile,
        string destFile,
        long fileSize,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await _loggingService.LogAsync(operation.Id, LogLevel.Debug,
                $"Using delta sync for: {Path.GetFileName(sourceFile)}");

            var signatureFile = destFile + ".sig";
            var deltaFile = destFile + ".delta";

            await using (var basisStream = new FileStream(destFile, FileMode.Open, FileAccess.Read))
            await using (var signatureStream = new FileStream(signatureFile, FileMode.Create, FileAccess.Write))
            {
                var signatureBuilder = new SignatureBuilder();
                await Task.Run(() => signatureBuilder.Build(basisStream, new SignatureWriter(signatureStream)), cancellationToken);
            }

            await using (var newFileStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
            await using (var signatureStream = new FileStream(signatureFile, FileMode.Open, FileAccess.Read))
            await using (var deltaStream = new FileStream(deltaFile, FileMode.Create, FileAccess.Write))
            {
                var deltaBuilder = new DeltaBuilder();
                await Task.Run(() => deltaBuilder.BuildDelta(newFileStream, new SignatureReader(signatureStream, null), new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(deltaStream))), cancellationToken);
            }

            var tempFile = destFile + ".tmp";
            await using (var basisStream = new FileStream(destFile, FileMode.Open, FileAccess.Read))
            await using (var deltaStream = new FileStream(deltaFile, FileMode.Open, FileAccess.Read))
            await using (var newFileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                var deltaApplier = new DeltaApplier();
                await Task.Run(() => deltaApplier.Apply(basisStream, new BinaryDeltaReader(deltaStream, null), newFileStream), cancellationToken);
            }

            File.Delete(destFile);
            File.Move(tempFile, destFile);

            if (File.Exists(signatureFile)) File.Delete(signatureFile);
            if (File.Exists(deltaFile)) File.Delete(deltaFile);

            var transferProgress = new FileTransferProgress
            {
                FileName = Path.GetFileName(sourceFile),
                SourcePath = sourceFile,
                DestinationPath = destFile,
                FileSize = fileSize,
                BytesTransferred = fileSize,
                CurrentSpeedBytesPerSecond = 0
            };

            progress?.Report(transferProgress);
            _progressTracker.UpdateFileProgress(operation.Id, transferProgress);
        }
        catch (Exception ex)
        {
            await _loggingService.LogAsync(operation.Id, LogLevel.Warning,
                $"Delta sync failed, falling back to direct copy: {ex.Message}");

            await CopyFileDirectAsync(operation, sourceFile, destFile, fileSize, progress, cancellationToken);
        }
    }

    private async Task CopyFileDirectAsync(
        CopyOperation operation,
        string sourceFile,
        string destFile,
        long fileSize,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 1024 * 1024; // 1 MB buffer
        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastReportTime = stopwatch.Elapsed;

        await using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous);
        await using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous);

        int bytesRead;
        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;

            if ((stopwatch.Elapsed - lastReportTime).TotalMilliseconds >= 100)
            {
                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                var currentSpeed = elapsedSeconds > 0 ? totalBytesRead / elapsedSeconds : 0;

                var transferProgress = new FileTransferProgress
                {
                    FileName = Path.GetFileName(sourceFile),
                    SourcePath = sourceFile,
                    DestinationPath = destFile,
                    FileSize = fileSize,
                    BytesTransferred = totalBytesRead,
                    CurrentSpeedBytesPerSecond = currentSpeed,
                    AverageSpeedBytesPerSecond = currentSpeed
                };

                progress?.Report(transferProgress);
                _progressTracker.UpdateFileProgress(operation.Id, transferProgress);
                lastReportTime = stopwatch.Elapsed;
            }
        }

        var finalProgress = new FileTransferProgress
        {
            FileName = Path.GetFileName(sourceFile),
            SourcePath = sourceFile,
            DestinationPath = destFile,
            FileSize = fileSize,
            BytesTransferred = totalBytesRead,
            CurrentSpeedBytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0 ? totalBytesRead / stopwatch.Elapsed.TotalSeconds : 0,
            AverageSpeedBytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0 ? totalBytesRead / stopwatch.Elapsed.TotalSeconds : 0
        };

        progress?.Report(finalProgress);
        _progressTracker.UpdateFileProgress(operation.Id, finalProgress);

        File.SetLastWriteTimeUtc(destFile, File.GetLastWriteTimeUtc(sourceFile));
    }

    private async Task CopyDirectoryAsync(
        CopyOperation operation,
        string sourceDir,
        string destDir,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destDir);

        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        foreach (var sourceFile in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            var destFile = Path.Combine(destDir, relativePath);

            await CopyFileAsync(operation, sourceFile, destFile, progress, cancellationToken);
        }
    }

    public void CancelOperation(string operationId)
    {
        if (_operations.TryGetValue(operationId, out var cts))
        {
            cts.Cancel();
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

        return $"{size:F2} {suffixes[suffixIndex]}";
    }
}
