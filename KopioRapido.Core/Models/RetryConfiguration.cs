namespace KopioRapido.Models;

public class RetryConfiguration
{
    /// <summary>
    /// Maximum number of retry attempts before giving up
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay before first retry in milliseconds
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay between retries in milliseconds
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 30000;

    /// <summary>
    /// Exponential backoff multiplier (default 2.0 = double each time)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Whether to add random jitter to retry delays (helps prevent thundering herd)
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Calculate the delay for a given retry attempt using exponential backoff
    /// </summary>
    public TimeSpan CalculateDelay(int attemptNumber)
    {
        var delay = InitialRetryDelayMs * Math.Pow(BackoffMultiplier, attemptNumber - 1);
        delay = Math.Min(delay, MaxRetryDelayMs);

        if (UseJitter)
        {
            var random = new Random();
            // Add random jitter of ±25%
            var jitter = delay * 0.25 * (random.NextDouble() * 2 - 1);
            delay += jitter;
        }

        return TimeSpan.FromMilliseconds(delay);
    }
}
