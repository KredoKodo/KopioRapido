using System.Collections.Concurrent;
using System.Text.Json;
using KopioRapido.Models;

namespace KopioRapido.Services;

public class LoggingService : ILoggingService
{
    private readonly string _logDirectory;
    private readonly ConcurrentDictionary<string, List<OperationLog>> _memoryLogs;
    private readonly SemaphoreSlim _fileLock;

    public LoggingService(string? customBaseDirectory = null)
    {
        var baseDir = customBaseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KopioRapido"
        );
        
        _logDirectory = Path.Combine(baseDir, "Logs");
        Directory.CreateDirectory(_logDirectory);

        _memoryLogs = new ConcurrentDictionary<string, List<OperationLog>>();
        _fileLock = new SemaphoreSlim(1, 1);
    }

    public async Task LogAsync(string operationId, LogLevel level, string message, string? filePath = null, Exception? exception = null)
    {
        var log = new OperationLog
        {
            OperationId = operationId,
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            FilePath = filePath,
            Exception = exception?.ToString()
        };

        _memoryLogs.AddOrUpdate(
            operationId,
            new List<OperationLog> { log },
            (key, list) =>
            {
                list.Add(log);
                return list;
            }
        );

        await WriteLogToFileAsync(operationId, log);
        Console.WriteLine($"[{level}] {message}");
    }

    public async Task<IEnumerable<OperationLog>> GetLogsAsync(string operationId)
    {
        if (_memoryLogs.TryGetValue(operationId, out var logs))
        {
            return logs;
        }
        return Enumerable.Empty<OperationLog>();
    }

    public async Task<IEnumerable<OperationLog>> GetLogsAsync(string operationId, DateTime startTime, DateTime endTime)
    {
        var allLogs = await GetLogsAsync(operationId);
        return allLogs.Where(l => l.Timestamp >= startTime && l.Timestamp <= endTime);
    }

    public async Task ClearLogsAsync(string operationId)
    {
        _memoryLogs.TryRemove(operationId, out _);

        var logFile = GetLogFilePath(operationId);
        if (File.Exists(logFile))
        {
            await _fileLock.WaitAsync();
            try
            {
                File.Delete(logFile);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }

    public async Task ExportLogsAsync(string operationId, string outputPath)
    {
        var logs = await GetLogsAsync(operationId);
        var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json);
    }

    private async Task WriteLogToFileAsync(string operationId, OperationLog log)
    {
        var logFile = GetLogFilePath(operationId);
        var logLine = $"{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{log.Level}] {log.Message}";

        if (!string.IsNullOrEmpty(log.FilePath))
        {
            logLine += $" | File: {log.FilePath}";
        }

        if (!string.IsNullOrEmpty(log.Exception))
        {
            logLine += $"\n    Exception: {log.Exception}";
        }

        logLine += Environment.NewLine;

        await _fileLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(logFile, logLine);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private string GetLogFilePath(string operationId)
    {
        return Path.Combine(_logDirectory, $"{operationId}.log");
    }
}
