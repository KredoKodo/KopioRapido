namespace KopioRapido.Models;

public class OperationLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OperationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? Exception { get; set; }
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
