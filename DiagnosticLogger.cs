using System.Diagnostics;

namespace KopioRapido;

/// <summary>
/// Diagnostic logger that writes to both Debug output and a crash log file.
/// Used to diagnose application startup crashes.
/// </summary>
public static class DiagnosticLogger
{
    private static readonly string LogFilePath;
    private static readonly object LockObject = new();

    static DiagnosticLogger()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDir = Path.Combine(appDataPath, "KopioRapido", "CrashLogs");
            Directory.CreateDirectory(logDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            LogFilePath = Path.Combine(logDir, $"startup_{timestamp}.log");

            Log("DiagnosticLogger initialized");
            Log($"Log file: {LogFilePath}");
            Log($"Process ID: {Environment.ProcessId}");
            Log($"OS: {Environment.OSVersion}");
            Log($".NET Version: {Environment.Version}");
        }
        catch (Exception ex)
        {
            // Fallback to temp directory if LocalApplicationData fails
            LogFilePath = Path.Combine(Path.GetTempPath(), $"KopioRapido_crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            Debug.WriteLine($"DiagnosticLogger: Failed to use AppData, using temp: {ex.Message}");
        }
    }

    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] {message}";

        // Write to Debug output
        Debug.WriteLine($"DiagnosticLogger: {logMessage}");

        // Write to file immediately with flush
        try
        {
            lock (LockObject)
            {
                File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DiagnosticLogger: Failed to write to file: {ex.Message}");
        }
    }

    public static void LogException(string context, Exception ex)
    {
        Log($"EXCEPTION in {context}: {ex.GetType().Name}: {ex.Message}");
        Log($"  Stack trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Log($"  Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
    }

    public static string GetLogFilePath() => LogFilePath;
}
