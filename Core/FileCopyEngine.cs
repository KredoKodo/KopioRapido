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
    private readonly IPerformanceMonitorService _performanceMonitor;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _operations;
    private readonly RetryConfiguration _retryConfig;
    private const long DELTA_THRESHOLD_BYTES = 10 * 1024 * 1024; // 10 MB

    public FileCopyEngine(
        ILoggingService loggingService,
        IProgressTrackerService progressTracker,
        IResumeService resumeService,
        IPerformanceMonitorService performanceMonitor)
    {
        _loggingService = loggingService;
        _progressTracker = progressTracker;
        _resumeService = resumeService;
        _performanceMonitor = performanceMonitor;
        _operations = new ConcurrentDictionary<string, CancellationTokenSource>();
        _retryConfig = new RetryConfiguration(); // Use default configuration
    }

    public FileCopyEngine(
        ILoggingService loggingService,
        IProgressTrackerService progressTracker,
        IResumeService resumeService,
        IPerformanceMonitorService performanceMonitor,
        RetryConfiguration retryConfig)
    {
        _loggingService = loggingService;
        _progressTracker = progressTracker;
        _resumeService = resumeService;
        _performanceMonitor = performanceMonitor;
        _operations = new ConcurrentDictionary<string, CancellationTokenSource>();
        _retryConfig = retryConfig;
    }

    public async Task<CopyOperation> CopyAsync(
        string sourcePath,
        string destinationPath,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        TransferStrategy? strategy = null)
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
                $"Starting copy operation from '{sourcePath}' to '{destinationPath}'").ConfigureAwait(false);

            if (strategy != null)
            {
                await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                    $"Using transfer strategy: {strategy.UserFriendlyDescription}").ConfigureAwait(false);
                await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                    $"Reasoning: {strategy.Reasoning}").ConfigureAwait(false);
            }

            // Use pre-calculated totals if available, otherwise analyze source
            if (strategy?.PreCalculatedTotalFiles.HasValue == true && strategy?.PreCalculatedTotalBytes.HasValue == true)
            {
                operation.TotalFiles = strategy.PreCalculatedTotalFiles.Value;
                operation.TotalBytes = strategy.PreCalculatedTotalBytes.Value;
                await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                    $"Using pre-calculated totals: {operation.TotalFiles} files, {FormatBytes(operation.TotalBytes)}").ConfigureAwait(false);
            }
            else
            {
                await AnalyzeSourceAsync(operation, sourcePath).ConfigureAwait(false);
            }

            _progressTracker.SetTotalSize(operation.Id, operation.TotalBytes, operation.TotalFiles);
            await _resumeService.SaveOperationStateAsync(operation).ConfigureAwait(false);

            if (File.Exists(sourcePath))
            {
                await CopyFileAsync(operation, sourcePath, destinationPath, progress, cts.Token).ConfigureAwait(false);
            }
            else if (Directory.Exists(sourcePath))
            {
                await CopyDirectoryAsync(operation, sourcePath, destinationPath, progress, cts.Token, strategy).ConfigureAwait(false);
            }
            else
            {
                throw new FileNotFoundException($"Source path not found: {sourcePath}");
            }

            operation.Status = CopyStatus.Completed;
            operation.EndTime = DateTime.UtcNow;

            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                $"Copy operation completed successfully. {operation.FilesTransferred} files, {FormatBytes(operation.BytesTransferred)} transferred").ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            operation.Status = CopyStatus.Cancelled;
            operation.EndTime = DateTime.UtcNow;
            await _loggingService.LogAsync(operation.Id, LogLevel.Warning, "Copy operation was cancelled").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            operation.Status = CopyStatus.Failed;
            operation.EndTime = DateTime.UtcNow;
            operation.ErrorMessage = ex.Message;
            await _loggingService.LogAsync(operation.Id, LogLevel.Error,
                $"Copy operation failed: {ex.Message}", exception: ex).ConfigureAwait(false);
        }
        finally
        {
            await _resumeService.SaveOperationStateAsync(operation).ConfigureAwait(false);
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
        CancellationToken cancellationToken,
        string? displayFileName = null,
        TransferStrategy? strategy = null)
    {
        var fileInfo = new FileInfo(sourceFile);
        var fileName = displayFileName ?? Path.GetFileName(sourceFile);

        operation.CurrentFile = fileName;
        await _resumeService.SaveOperationStateAsync(operation).ConfigureAwait(false);

        await _loggingService.LogAsync(operation.Id, LogLevel.Info,
            $"Copying file: {fileName} ({FormatBytes(fileInfo.Length)})", sourceFile).ConfigureAwait(false);

        var destDir = Path.GetDirectoryName(destFile);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        var stopwatch = Stopwatch.StartNew();

        // Check if we should use compression for this file
        bool useCompression = strategy?.UseCompression == true && 
                              CompressionHelper.ShouldCompressFile(sourceFile);
        
        if (useCompression)
        {
            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                $"Using compression for: {fileName}");
        }

        // Wrap the copy operation with retry logic
        await RetryHelper.ExecuteWithRetryAsync(
            async (attempt, ct) =>
            {
                // If compression is enabled, use it instead of delta sync
                if (useCompression)
                {
                    await CopyFileWithCompressionAsync(operation, sourceFile, destFile, fileInfo.Length, progress, attempt, ct, fileName);
                }
                else
                {
                    bool useDeltaSync = ShouldUseDeltaSync(sourceFile, destFile, out bool isPartialFile);

                    if (isPartialFile)
                    {
                        await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                            $"Detected partial file transfer, using delta sync to resume: {fileName}");
                    }

                    if (useDeltaSync)
                    {
                        await CopyFileWithDeltaSyncAsync(operation, sourceFile, destFile, fileInfo.Length, progress, attempt, ct, fileName);
                    }
                    else
                    {
                        await CopyFileDirectAsync(operation, sourceFile, destFile, fileInfo.Length, progress, attempt, ct, fileName);
                    }
                }
            },
            _retryConfig,
            onRetry: async (attemptNumber, exception, delay) =>
            {
                await _loggingService.LogAsync(operation.Id, LogLevel.Warning,
                    $"Retry attempt {attemptNumber}/{_retryConfig.MaxRetryAttempts} for file '{fileName}' after error: {exception.Message}. Waiting {delay.TotalSeconds:F1}s before retry.");

                // Report retry status to UI
                progress?.Report(new FileTransferProgress
                {
                    OperationId = operation.Id,
                    FileName = fileName,
                    SourcePath = sourceFile,
                    DestinationPath = destFile,
                    FileSize = fileInfo.Length,
                    RetryAttempt = attemptNumber,
                    MaxRetryAttempts = _retryConfig.MaxRetryAttempts,
                    IsRetrying = true,
                    LastError = exception.Message
                });
            },
            cancellationToken);

        stopwatch.Stop();

        operation.FilesTransferred++;
        operation.BytesTransferred += fileInfo.Length;
        _progressTracker.UpdateOverallProgress(operation.Id, operation.BytesTransferred, operation.FilesTransferred);
        operation.CurrentFile = null;

        await _loggingService.LogAsync(operation.Id, LogLevel.Info,
            $"File copied successfully: {fileName} in {stopwatch.Elapsed.TotalSeconds:F2}s", sourceFile);
    }

    private bool ShouldUseDeltaSync(string sourceFile, string destFile, out bool isPartialFile)
    {
        isPartialFile = false;

        if (!File.Exists(destFile))
        {
            return false;
        }

        var sourceInfo = new FileInfo(sourceFile);
        var destInfo = new FileInfo(destFile);

        // Always use delta sync for partial files (incomplete transfers)
        // This helps resume interrupted file copies efficiently
        if (destInfo.Length > 0 && destInfo.Length < sourceInfo.Length)
        {
            isPartialFile = true;
            return true;
        }

        // For complete files, only use delta sync if they're large enough
        if (sourceInfo.Length < DELTA_THRESHOLD_BYTES)
        {
            return false;
        }

        // Skip delta sync if files are already identical
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
        int attemptNumber,
        CancellationToken cancellationToken,
        string fileName)
    {
        try
        {
            await _loggingService.LogAsync(operation.Id, LogLevel.Debug,
                $"Using delta sync for: {fileName}");

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
                OperationId = operation.Id,
                FileName = fileName,
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

            await CopyFileDirectAsync(operation, sourceFile, destFile, fileSize, progress, attemptNumber, cancellationToken, fileName);
        }
    }

    private async Task CopyFileDirectAsync(
        CopyOperation operation,
        string sourceFile,
        string destFile,
        long fileSize,
        IProgress<FileTransferProgress>? progress,
        int attemptNumber,
        CancellationToken cancellationToken,
        string fileName)
    {
        const int bufferSize = 8 * 1024 * 1024; // 8 MB buffer for better performance
        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastReportTime = stopwatch.Elapsed;

        await using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        int bytesRead;
        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;

            // Report progress every 500ms instead of 100ms to reduce overhead
            if ((stopwatch.Elapsed - lastReportTime).TotalMilliseconds >= 500)
            {
                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                var currentSpeed = elapsedSeconds > 0 ? totalBytesRead / elapsedSeconds : 0;

                var transferProgress = new FileTransferProgress
                {
                    OperationId = operation.Id,
                    FileName = fileName,
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
            OperationId = operation.Id,
            FileName = fileName,
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
    
    private async Task CopyFileWithCompressionAsync(
        CopyOperation operation,
        string sourceFile,
        string destFile,
        long fileSize,
        IProgress<FileTransferProgress>? progress,
        int attemptNumber,
        CancellationToken cancellationToken,
        string fileName)
    {
        // Compression for network transfers: compress → transfer → decompress
        // The destination file will be identical to source (not compressed)
        // Bandwidth savings come from compressed data during transfer
        
        var stopwatch = Stopwatch.StartNew();
        long totalCompressedBytes = 0;
        long totalUncompressedBytes = 0;
        var lastReportTime = stopwatch.Elapsed;
        
        // Create temporary compressed file for intermediate storage
        var tempCompressedFile = destFile + ".tmp.br";
        
        try
        {
            // Step 1: Compress source to temp file
            const int bufferSize = 1024 * 1024; // 1 MB buffer
            var buffer = new byte[bufferSize];
            
            await using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
            await using (var compressedStream = new FileStream(tempCompressedFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
            await using (var compressionStream = new System.IO.Compression.BrotliStream(compressedStream, System.IO.Compression.CompressionLevel.Fastest))
            {
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await compressionStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    
                    totalUncompressedBytes += bytesRead;
                    totalCompressedBytes = compressedStream.Position;
                    
                    // Report compression progress every 500ms
                    if ((stopwatch.Elapsed - lastReportTime).TotalMilliseconds >= 500)
                    {
                        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                        var currentSpeed = elapsedSeconds > 0 ? totalUncompressedBytes / elapsedSeconds : 0;
                        var compressionRatio = totalCompressedBytes > 0 ? (double)totalUncompressedBytes / totalCompressedBytes : 1.0;
                        
                        var transferProgress = new FileTransferProgress
                        {
                            OperationId = operation.Id,
                            FileName = fileName,
                            SourcePath = sourceFile,
                            DestinationPath = destFile,
                            FileSize = fileSize,
                            BytesTransferred = totalUncompressedBytes,
                            CompressedBytesTransferred = totalCompressedBytes,
                            IsCompressed = true,
                            CompressionRatio = compressionRatio,
                            CurrentSpeedBytesPerSecond = currentSpeed,
                            AverageSpeedBytesPerSecond = currentSpeed
                        };
                        
                        progress?.Report(transferProgress);
                        _progressTracker.UpdateFileProgress(operation.Id, transferProgress);
                        lastReportTime = stopwatch.Elapsed;
                    }
                }
            }
            
            var compressionElapsed = stopwatch.Elapsed;
            var finalCompressionRatio = totalCompressedBytes > 0 ? (double)totalUncompressedBytes / totalCompressedBytes : 1.0;
            var savedBytes = totalUncompressedBytes - totalCompressedBytes;
            var savedPercentage = totalUncompressedBytes > 0 ? (savedBytes * 100.0 / totalUncompressedBytes) : 0;
            
            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                $"Compressed {fileName}: {finalCompressionRatio:F2}x ratio, saved {FormatBytes(savedBytes)} ({savedPercentage:F1}%) - {compressionElapsed.TotalSeconds:F2}s");
            
            // Step 2: Decompress to final destination
            // In a real network scenario, this happens on the remote machine
            // For local testing, we simulate it by decompressing immediately
            await using (var compressedStream = new FileStream(tempCompressedFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
            await using (var decompressionStream = new System.IO.Compression.BrotliStream(compressedStream, System.IO.Compression.CompressionMode.Decompress))
            await using (var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
            {
                await decompressionStream.CopyToAsync(destStream, bufferSize, cancellationToken);
            }
            
            stopwatch.Stop();
            
            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                $"Decompressed {fileName} - Total time: {stopwatch.Elapsed.TotalSeconds:F2}s (compression: {compressionElapsed.TotalSeconds:F2}s)");
            
            // Report final progress
            var finalProgress = new FileTransferProgress
            {
                OperationId = operation.Id,
                FileName = fileName,
                SourcePath = sourceFile,
                DestinationPath = destFile,
                FileSize = fileSize,
                BytesTransferred = fileSize,
                CompressedBytesTransferred = totalCompressedBytes,
                IsCompressed = true,
                CompressionRatio = finalCompressionRatio,
                CurrentSpeedBytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0 ? fileSize / stopwatch.Elapsed.TotalSeconds : 0,
                AverageSpeedBytesPerSecond = stopwatch.Elapsed.TotalSeconds > 0 ? fileSize / stopwatch.Elapsed.TotalSeconds : 0
            };
            
            progress?.Report(finalProgress);
            _progressTracker.UpdateFileProgress(operation.Id, finalProgress);
            
            File.SetLastWriteTimeUtc(destFile, File.GetLastWriteTimeUtc(sourceFile));
        }
        finally
        {
            // Clean up temporary compressed file
            if (File.Exists(tempCompressedFile))
            {
                try { File.Delete(tempCompressedFile); } catch { }
            }
        }
    }

    private async Task CopyDirectoryAsync(
        CopyOperation operation,
        string sourceDir,
        string destDir,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken,
        TransferStrategy? strategy = null)
    {
        Directory.CreateDirectory(destDir);

        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        int skippedFiles = 0;

        // Use strategy to determine if parallel or sequential
        bool useParallel = strategy != null && strategy.MaxConcurrentFiles > 1;
        
        if (useParallel)
        {
            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                $"Using parallel mode with {strategy!.MaxConcurrentFiles} concurrent files");
            
            await CopyFilesParallelAsync(operation, sourceDir, destDir, files, progress, cancellationToken, strategy);
        }
        else
        {
            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                "Using sequential mode");
            
            await CopyFilesSequentialAsync(operation, sourceDir, destDir, files, progress, cancellationToken, strategy);
        }

        if (skippedFiles > 0)
        {
            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                $"Skipped {skippedFiles} already completed files");
        }
    }
    
    private async Task CopyFilesSequentialAsync(
        CopyOperation operation,
        string sourceDir,
        string destDir,
        string[] files,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken,
        TransferStrategy? strategy = null)
    {
        foreach (var sourceFile in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            var destFile = Path.Combine(destDir, relativePath);

            // Check if file was already completed in a previous run
            if (IsFileAlreadyCompleted(operation, sourceFile, destFile, sourceDir))
            {
                await _loggingService.LogAsync(operation.Id, LogLevel.Debug,
                    $"Skipping already completed file: {relativePath}");
                continue;
            }

            await CopyFileAsync(operation, sourceFile, destFile, progress, cancellationToken, relativePath, strategy);

            // Mark file as completed for resume purposes
            MarkFileAsCompleted(operation, sourceFile, sourceDir);

            // Periodically save operation state (every 10 files)
            if (operation.FilesTransferred % 10 == 0)
            {
                await _resumeService.SaveOperationStateAsync(operation);
            }
        }
    }
    
    private async Task CopyFilesParallelAsync(
        CopyOperation operation,
        string sourceDir,
        string destDir,
        string[] files,
        IProgress<FileTransferProgress>? progress,
        CancellationToken cancellationToken,
        TransferStrategy strategy)
    {
        // Start performance monitoring
        _performanceMonitor.StartMonitoring(operation.Id, strategy.MaxConcurrentFiles);
        
        var semaphore = new SemaphoreSlim(strategy.MaxConcurrentFiles);
        var tasks = new List<Task>();
        var errors = new ConcurrentBag<(string file, Exception error)>();
        var activeConcurrency = 0;
        var lastAdaptationCheck = DateTime.UtcNow;
        var samplesRecorded = 0;
        
        // Performance monitoring timer
        var performanceTimer = new System.Timers.Timer(2000); // Check every 2 seconds
        performanceTimer.Elapsed += async (sender, e) =>
        {
            try
            {
                // Record current performance sample
                var currentSpeed = _progressTracker.GetCurrentSpeed(operation.Id);
                if (currentSpeed > 0)
                {
                    var speedMBps = currentSpeed / (1024.0 * 1024.0);
                    _performanceMonitor.RecordSample(operation.Id, speedMBps, Interlocked.CompareExchange(ref activeConcurrency, 0, 0));
                    samplesRecorded++;
                }
                
                // Check if we should adapt (every 5 seconds minimum)
                if ((DateTime.UtcNow - lastAdaptationCheck).TotalSeconds >= 5 && samplesRecorded >= 3)
                {
                    var (shouldAdjust, newConcurrency, reason) = _performanceMonitor.ShouldAdjustConcurrency(operation.Id);
                    
                    if (shouldAdjust && newConcurrency != semaphore.CurrentCount + activeConcurrency)
                    {
                        await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                            $"🎯 Adaptive Optimization: {reason}");
                        
                        // Adjust semaphore capacity
                        int currentCapacity = strategy.MaxConcurrentFiles;
                        int difference = newConcurrency - currentCapacity;
                        
                        if (difference > 0)
                        {
                            // Increase capacity
                            for (int i = 0; i < difference; i++)
                                semaphore.Release();
                        }
                        // Note: We can't decrease semaphore capacity mid-operation safely,
                        // but we can prevent new tasks from starting by tracking it
                        
                        strategy.MaxConcurrentFiles = newConcurrency;
                        _performanceMonitor.RecordAdaptation(operation.Id, newConcurrency, reason);
                        lastAdaptationCheck = DateTime.UtcNow;
                    }
                }
            }
            catch
            {
                // Ignore timer errors
            }
        };
        performanceTimer.Start();
        
        
        try
        {
            foreach (var sourceFile in files)
            {
                var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
                var destFile = Path.Combine(destDir, relativePath);
                
                // Check if already completed
            if (IsFileAlreadyCompleted(operation, sourceFile, destFile, sourceDir))
            {
                await _loggingService.LogAsync(operation.Id, LogLevel.Debug,
                    $"Skipping already completed file: {relativePath}");
                continue;
            }
            
            // Create task for this file
            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                Interlocked.Increment(ref activeConcurrency);
                try
                {
                    await CopyFileAsync(operation, sourceFile, destFile, progress, cancellationToken, relativePath, strategy);
                    
                    // Mark as completed
                    MarkFileAsCompleted(operation, sourceFile, sourceDir);
                    
                    // Periodically save state
                    if (operation.FilesTransferred % 10 == 0)
                    {
                        await _resumeService.SaveOperationStateAsync(operation);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors.Add((relativePath, ex));
                    await _loggingService.LogAsync(operation.Id, LogLevel.Error,
                        $"Error copying {relativePath}: {ex.Message}");
                }
                finally
                {
                    Interlocked.Decrement(ref activeConcurrency);
                    semaphore.Release();
                }
            }, cancellationToken);
            
            tasks.Add(task);
        }
        
        // Wait for all files to complete
        await Task.WhenAll(tasks);
        
        // Report any errors
        if (!errors.IsEmpty)
        {
            await _loggingService.LogAsync(operation.Id, LogLevel.Warning,
                $"{errors.Count} files failed to copy");
        }
        }
        finally
        {
            // Stop performance monitoring and timer
            performanceTimer.Stop();
            performanceTimer.Dispose();
            _performanceMonitor.StopMonitoring(operation.Id);
        }
    }

    public void CancelOperation(string operationId)
    {
        if (_operations.TryGetValue(operationId, out var cts))
        {
            cts.Cancel();
        }
    }

    public async Task<CopyOperation> ResumeAsync(
        string operationId,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = await _resumeService.LoadOperationStateAsync(operationId);
        if (operation == null)
        {
            throw new InvalidOperationException($"Operation {operationId} not found");
        }

        if (!await _resumeService.CanResumeAsync(operationId))
        {
            throw new InvalidOperationException($"Operation {operationId} cannot be resumed");
        }

        await _loggingService.LogAsync(operation.Id, LogLevel.Info,
            $"Resuming copy operation from '{operation.SourcePath}' to '{operation.DestinationPath}'. " +
            $"Already completed: {operation.FilesTransferred}/{operation.TotalFiles} files ({FormatBytes(operation.BytesTransferred)}/{FormatBytes(operation.TotalBytes)})");

        operation.Status = CopyStatus.InProgress;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _operations.TryAdd(operation.Id, cts);

        try
        {
            _progressTracker.SetTotalSize(operation.Id, operation.TotalBytes, operation.TotalFiles);
            _progressTracker.SetProgress(operation.Id, operation.BytesTransferred, operation.FilesTransferred);

            if (File.Exists(operation.SourcePath))
            {
                await CopyFileAsync(operation, operation.SourcePath, operation.DestinationPath, progress, cts.Token);
            }
            else if (Directory.Exists(operation.SourcePath))
            {
                await CopyDirectoryAsync(operation, operation.SourcePath, operation.DestinationPath, progress, cts.Token);
            }
            else
            {
                throw new FileNotFoundException($"Source path not found: {operation.SourcePath}");
            }

            operation.Status = CopyStatus.Completed;
            operation.EndTime = DateTime.UtcNow;

            await _loggingService.LogAsync(operation.Id, LogLevel.Info,
                $"Resume copy operation completed successfully. {operation.FilesTransferred} files, {FormatBytes(operation.BytesTransferred)} transferred");
        }
        catch (OperationCanceledException)
        {
            operation.Status = CopyStatus.Cancelled;
            operation.EndTime = DateTime.UtcNow;
            await _loggingService.LogAsync(operation.Id, LogLevel.Warning, "Resume operation was cancelled");
        }
        catch (Exception ex)
        {
            operation.Status = CopyStatus.Failed;
            operation.ErrorMessage = ex.Message;
            operation.EndTime = DateTime.UtcNow;
            await _loggingService.LogAsync(operation.Id, LogLevel.Error,
                $"Resume operation failed: {ex.Message}");
        }
        finally
        {
            await _resumeService.SaveOperationStateAsync(operation);
            _operations.TryRemove(operation.Id, out _);
        }

        return operation;
    }

    private bool IsFileAlreadyCompleted(CopyOperation operation, string sourceFile, string destFile, string sourceDir)
    {
        var relativePath = Path.GetRelativePath(sourceDir, sourceFile);

        // Check if file was already completed in a previous run
        var completedFile = operation.CompletedFiles.FirstOrDefault(f => f.RelativePath == relativePath);
        if (completedFile == null)
        {
            return false;
        }

        // Verify destination file still exists
        if (!File.Exists(destFile))
        {
            // Destination was deleted, need to recopy
            operation.CompletedFiles.Remove(completedFile);
            return false;
        }

        // Verify file integrity (size and last modified time match)
        var sourceInfo = new FileInfo(sourceFile);
        var destInfo = new FileInfo(destFile);

        if (sourceInfo.Length != completedFile.FileSize ||
            sourceInfo.LastWriteTimeUtc != completedFile.LastModified)
        {
            // Source file changed, need to recopy
            operation.CompletedFiles.Remove(completedFile);
            return false;
        }

        // Verify destination file has same size
        if (destInfo.Length != completedFile.FileSize)
        {
            // Destination file corrupted, need to recopy
            operation.CompletedFiles.Remove(completedFile);
            return false;
        }

        return true;
    }

    private void MarkFileAsCompleted(CopyOperation operation, string sourceFile, string sourceDir)
    {
        var sourceInfo = new FileInfo(sourceFile);
        var relativePath = Path.GetRelativePath(sourceDir, sourceFile);

        var completedFile = new CompletedFileInfo
        {
            RelativePath = relativePath,
            FileSize = sourceInfo.Length,
            LastModified = sourceInfo.LastWriteTimeUtc,
            CompletedAt = DateTime.UtcNow
        };

        // Remove if already exists (shouldn't happen, but be safe)
        operation.CompletedFiles.RemoveAll(f => f.RelativePath == relativePath);
        operation.CompletedFiles.Add(completedFile);
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
