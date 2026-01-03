using KopioRapido.Models;

namespace KopioRapido.Services;

public interface ILoggingService
{
    Task LogAsync(string operationId, LogLevel level, string message, string? filePath = null, Exception? exception = null);
    Task<IEnumerable<OperationLog>> GetLogsAsync(string operationId);
    Task<IEnumerable<OperationLog>> GetLogsAsync(string operationId, DateTime startTime, DateTime endTime);
    Task ClearLogsAsync(string operationId);
    Task ExportLogsAsync(string operationId, string outputPath);
}
