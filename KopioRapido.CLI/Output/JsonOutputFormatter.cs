using System.Text.Json;
using KopioRapido.Models;

namespace KopioRapido.CLI.Output;

public class JsonOutputFormatter : IOutputFormatter
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void ShowAnalysis(StorageProfile source, StorageProfile dest,
                            FileSetProfile files, TransferStrategy strategy)
    {
        var compressiblePercent = files.TotalFiles > 0 ? (files.CompressibleFiles * 100.0 / files.TotalFiles) : 0;
        
        var analysis = new
        {
            source = new
            {
                path = source.Path,
                type = source.Type.ToString(),
                writeSpeedMBps = source.SequentialWriteMBps,
                isRemote = source.IsRemote
            },
            destination = new
            {
                path = dest.Path,
                type = dest.Type.ToString(),
                writeSpeedMBps = dest.SequentialWriteMBps,
                isRemote = dest.IsRemote
            },
            files = new
            {
                totalFiles = files.TotalFiles,
                totalSizeBytes = files.TotalBytes,
                averageFileSizeMB = files.AverageFileSizeMB,
                largeFilesCount = files.LargeFiles,
                compressiblePercent
            },
            strategy = new
            {
                mode = strategy.Mode.ToString(),
                description = strategy.UserFriendlyDescription,
                maxConcurrentFiles = strategy.MaxConcurrentFiles,
                bufferSizeKB = strategy.BufferSizeKB,
                useCompression = strategy.UseCompression,
                useDeltaSync = strategy.UseDeltaSync,
                reasoning = strategy.Reasoning
            }
        };
        Console.WriteLine(JsonSerializer.Serialize(analysis, _options));
    }

    public void ShowSyncSummary(SyncOperationSummary summary)
    {
        Console.WriteLine(JsonSerializer.Serialize(summary, _options));
    }

    public void ShowProgress(FileTransferProgress progress)
    {
        // For JSON mode, only output final result, not intermediate progress
    }

    public void ShowResult(CopyOperation operation)
    {
        var elapsedTime = operation.EndTime.HasValue 
            ? operation.EndTime.Value - operation.StartTime 
            : TimeSpan.Zero;
        var averageSpeedMBps = elapsedTime.TotalSeconds > 0
            ? operation.BytesTransferred / (1024.0 * 1024.0) / elapsedTime.TotalSeconds
            : 0;
        var bytesSaved = operation.TotalUncompressedBytes - operation.TotalCompressedBytes;
        var compressionRatio = operation.TotalCompressedBytes > 0
            ? operation.TotalUncompressedBytes / (double)operation.TotalCompressedBytes
            : 1.0;

        var result = new
        {
            operationId = operation.Id,
            operationType = operation.OperationType.ToString(),
            status = operation.Status.ToString(),
            sourcePath = operation.SourcePath,
            destinationPath = operation.DestinationPath,
            filesTransferred = operation.FilesTransferred,
            bytesTransferred = operation.BytesTransferred,
            bytesSaved,
            filesCompressed = operation.FilesCompressed,
            compressionRatio,
            elapsedTime = elapsedTime.ToString(),
            averageSpeedMBps,
            errorMessage = operation.ErrorMessage
        };
        Console.WriteLine(JsonSerializer.Serialize(result, _options));
    }

    public void Error(string message)
    {
        var error = new { level = "error", message };
        Console.Error.WriteLine(JsonSerializer.Serialize(error, _options));
    }

    public void Warning(string message)
    {
        var warning = new { level = "warning", message };
        Console.WriteLine(JsonSerializer.Serialize(warning, _options));
    }

    public void Info(string message)
    {
        var info = new { level = "info", message };
        Console.WriteLine(JsonSerializer.Serialize(info, _options));
    }
}
