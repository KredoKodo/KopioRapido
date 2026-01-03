namespace KopioRapido.Models;

public class PerformanceMetrics
{
    public string OperationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Speed metrics (MB/s)
    public double CurrentSpeedMBps { get; set; }
    public double AverageSpeedMBps { get; set; }
    public double PeakSpeedMBps { get; set; }
    public double MovingAverageSpeedMBps { get; set; }
    
    // Concurrency metrics
    public int CurrentConcurrency { get; set; }
    public int OptimalConcurrency { get; set; }
    public int ActiveTransfers { get; set; }
    
    // Efficiency metrics
    public double EfficiencyRatio { get; set; } // Current speed / Peak speed
    public bool IsBottlenecked { get; set; }
    public BottleneckType? BottleneckType { get; set; }
    
    // Trend detection
    public SpeedTrend Trend { get; set; }
    public bool ShouldIncreaseParallelism { get; set; }
    public bool ShouldDecreaseParallelism { get; set; }
    
    // Adaptation history
    public int AdaptationCount { get; set; }
    public DateTime? LastAdaptation { get; set; }
    public string? LastAdaptationReason { get; set; }
}

public enum BottleneckType
{
    None,
    DiskIO,
    NetworkBandwidth,
    CPU,
    Unknown
}

public enum SpeedTrend
{
    Increasing,
    Stable,
    Decreasing,
    Volatile
}
