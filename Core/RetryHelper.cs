using KopioRapido.Models;

namespace KopioRapido.Core;

public static class RetryHelper
{
    /// <summary>
    /// Determines if an exception is transient and worth retrying
    /// </summary>
    public static bool IsTransientError(Exception ex)
    {
        return ex switch
        {
            // Network/IO errors that are typically transient
            IOException ioEx when IsTransientIOException(ioEx) => true,
            UnauthorizedAccessException => true, // File might be temporarily locked
            TimeoutException => true,

            // Specific error messages that indicate transient issues
            _ when ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) => true,
            _ when ex.Message.Contains("network path", StringComparison.OrdinalIgnoreCase) => true,
            _ when ex.Message.Contains("network name", StringComparison.OrdinalIgnoreCase) => true,
            _ when ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) => true,
            _ when ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,

            // Default to not retrying unknown errors
            _ => false
        };
    }

    private static bool IsTransientIOException(IOException ioEx)
    {
        const int ERROR_SHARING_VIOLATION = 32;
        const int ERROR_LOCK_VIOLATION = 33;
        const int ERROR_NETWORK_BUSY = 54;
        const int ERROR_NETWORK_ACCESS_DENIED = 65;
        const int ERROR_BAD_NET_NAME = 67;
        const int ERROR_UNEXP_NET_ERR = 59;

        var hResult = ioEx.HResult & 0xFFFF;

        return hResult == ERROR_SHARING_VIOLATION ||
               hResult == ERROR_LOCK_VIOLATION ||
               hResult == ERROR_NETWORK_BUSY ||
               hResult == ERROR_NETWORK_ACCESS_DENIED ||
               hResult == ERROR_BAD_NET_NAME ||
               hResult == ERROR_UNEXP_NET_ERR;
    }

    /// <summary>
    /// Executes an async function with retry logic and exponential backoff
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<int, CancellationToken, Task<T>> operation,
        RetryConfiguration config,
        Action<int, Exception, TimeSpan>? onRetry = null,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= config.MaxRetryAttempts; attempt++)
        {
            try
            {
                return await operation(attempt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Don't retry if the operation was cancelled
                throw;
            }
            catch (Exception ex) when (attempt < config.MaxRetryAttempts && IsTransientError(ex))
            {
                lastException = ex;
                var delay = config.CalculateDelay(attempt + 1);

                onRetry?.Invoke(attempt + 1, ex, delay);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // If we get here, all retries failed
        throw new AggregateException(
            $"Operation failed after {config.MaxRetryAttempts} retry attempts",
            lastException ?? new Exception("Unknown error"));
    }

    /// <summary>
    /// Executes an async action with retry logic and exponential backoff
    /// </summary>
    public static async Task ExecuteWithRetryAsync(
        Func<int, CancellationToken, Task> operation,
        RetryConfiguration config,
        Action<int, Exception, TimeSpan>? onRetry = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync<object?>(
            async (attempt, ct) =>
            {
                await operation(attempt, ct);
                return null;
            },
            config,
            onRetry,
            cancellationToken);
    }
}
