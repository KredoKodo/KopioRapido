using System.Collections.Concurrent;
using KopioRapido.Models;

namespace KopioRapido.Services;

public class PerformanceMonitorService : IPerformanceMonitorService
{
    private readonly ConcurrentDictionary<string, OperationMonitor> _monitors = new();
    private const int SampleWindowSize = 10; // Track last 10 samples
    private const int MinSamplesBeforeAdaptation = 5;
    private const double DegradationThreshold = 0.7; // 30% speed drop = degradation
    private const double ImprovementThreshold = 1.2; // 20% speed increase = can add more
    private const int MinSecondsBetweenAdaptations = 5;
    
    private class OperationMonitor
    {
        public string OperationId { get; set; } = string.Empty;
        public int InitialConcurrency { get; set; }
        public int CurrentConcurrency { get; set; }
        public Queue<SpeedSample> Samples { get; set; } = new();
        public double PeakSpeed { get; set; }
        public int AdaptationCount { get; set; }
        public DateTime? LastAdaptation { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
    }
    
    private class SpeedSample
    {
        public DateTime Timestamp { get; set; }
        public double SpeedMBps { get; set; }
        public int Concurrency { get; set; }
    }
    
    public void StartMonitoring(string operationId, int initialConcurrency)
    {
        var monitor = new OperationMonitor
        {
            OperationId = operationId,
            InitialConcurrency = initialConcurrency,
            CurrentConcurrency = initialConcurrency,
            StartTime = DateTime.UtcNow
        };
        
        _monitors.TryAdd(operationId, monitor);
    }
    
    public void StopMonitoring(string operationId)
    {
        _monitors.TryRemove(operationId, out _);
    }
    
    public void RecordSample(string operationId, double speedMBps, int activeConcurrency)
    {
        if (!_monitors.TryGetValue(operationId, out var monitor))
            return;
        
        var sample = new SpeedSample
        {
            Timestamp = DateTime.UtcNow,
            SpeedMBps = speedMBps,
            Concurrency = activeConcurrency
        };
        
        monitor.Samples.Enqueue(sample);
        
        // Keep only last N samples
        while (monitor.Samples.Count > SampleWindowSize)
            monitor.Samples.Dequeue();
        
        // Track peak speed
        if (speedMBps > monitor.PeakSpeed)
            monitor.PeakSpeed = speedMBps;
    }
    
    public PerformanceMetrics? GetMetrics(string operationId)
    {
        if (!_monitors.TryGetValue(operationId, out var monitor))
            return null;
        
        if (monitor.Samples.Count == 0)
            return null;
        
        var samples = monitor.Samples.ToArray();
        var currentSpeed = samples.Last().SpeedMBps;
        var averageSpeed = samples.Average(s => s.SpeedMBps);
        var movingAverage = samples.TakeLast(5).Average(s => s.SpeedMBps);
        
        var trend = DetectTrend(samples);
        var efficiency = monitor.PeakSpeed > 0 ? currentSpeed / monitor.PeakSpeed : 1.0;
        
        return new PerformanceMetrics
        {
            OperationId = operationId,
            CurrentSpeedMBps = currentSpeed,
            AverageSpeedMBps = averageSpeed,
            PeakSpeedMBps = monitor.PeakSpeed,
            MovingAverageSpeedMBps = movingAverage,
            CurrentConcurrency = monitor.CurrentConcurrency,
            ActiveTransfers = samples.Last().Concurrency,
            EfficiencyRatio = efficiency,
            IsBottlenecked = efficiency < DegradationThreshold,
            Trend = trend,
            AdaptationCount = monitor.AdaptationCount,
            LastAdaptation = monitor.LastAdaptation
        };
    }
    
    public (bool shouldAdjust, int newConcurrency, string reason) ShouldAdjustConcurrency(string operationId)
    {
        if (!_monitors.TryGetValue(operationId, out var monitor))
            return (false, 0, string.Empty);
        
        // Need enough samples to make a decision
        if (monitor.Samples.Count < MinSamplesBeforeAdaptation)
            return (false, monitor.CurrentConcurrency, "Not enough data");
        
        // Don't adapt too frequently
        if (monitor.LastAdaptation.HasValue && 
            (DateTime.UtcNow - monitor.LastAdaptation.Value).TotalSeconds < MinSecondsBetweenAdaptations)
            return (false, monitor.CurrentConcurrency, "Too soon since last adaptation");
        
        var metrics = GetMetrics(operationId);
        if (metrics == null)
            return (false, monitor.CurrentConcurrency, "No metrics available");
        
        // Check for degradation - reduce concurrency
        if (metrics.EfficiencyRatio < DegradationThreshold && monitor.CurrentConcurrency > 1)
        {
            int newConcurrency = Math.Max(1, (int)(monitor.CurrentConcurrency * 0.75)); // Reduce by 25%
            string reason = $"Performance degraded ({metrics.EfficiencyRatio:P0} efficiency) - reducing from {monitor.CurrentConcurrency} to {newConcurrency}";
            return (true, newConcurrency, reason);
        }
        
        // Check for improvement potential - increase concurrency
        if (metrics.Trend == SpeedTrend.Increasing && 
            metrics.CurrentSpeedMBps > metrics.AverageSpeedMBps * ImprovementThreshold &&
            monitor.CurrentConcurrency < 32) // Max limit
        {
            int newConcurrency = Math.Min(32, monitor.CurrentConcurrency + 2); // Increase by 2
            string reason = $"Speed improving ({metrics.CurrentSpeedMBps:F1} MB/s) - increasing from {monitor.CurrentConcurrency} to {newConcurrency}";
            return (true, newConcurrency, reason);
        }
        
        // Check if we're at low concurrency but speed is stable - try increasing
        if (monitor.CurrentConcurrency < 4 && 
            metrics.Trend == SpeedTrend.Stable &&
            monitor.AdaptationCount == 0) // Only try this once at the beginning
        {
            int newConcurrency = Math.Min(8, monitor.CurrentConcurrency * 2);
            string reason = $"Low concurrency with stable speed - testing increase to {newConcurrency}";
            return (true, newConcurrency, reason);
        }
        
        return (false, monitor.CurrentConcurrency, "Performance optimal");
    }
    
    public void RecordAdaptation(string operationId, int newConcurrency, string reason)
    {
        if (!_monitors.TryGetValue(operationId, out var monitor))
            return;
        
        monitor.CurrentConcurrency = newConcurrency;
        monitor.LastAdaptation = DateTime.UtcNow;
        monitor.AdaptationCount++;
    }
    
    private SpeedTrend DetectTrend(SpeedSample[] samples)
    {
        if (samples.Length < 3)
            return SpeedTrend.Stable;
        
        var recentSamples = samples.TakeLast(5).ToArray();
        var speeds = recentSamples.Select(s => s.SpeedMBps).ToArray();
        
        // Calculate linear regression slope
        double avgX = (speeds.Length - 1) / 2.0;
        double avgY = speeds.Average();
        
        double numerator = 0;
        double denominator = 0;
        
        for (int i = 0; i < speeds.Length; i++)
        {
            numerator += (i - avgX) * (speeds[i] - avgY);
            denominator += (i - avgX) * (i - avgX);
        }
        
        double slope = denominator != 0 ? numerator / denominator : 0;
        
        // Check variance for volatility
        double variance = speeds.Select(s => Math.Pow(s - avgY, 2)).Average();
        double stdDev = Math.Sqrt(variance);
        double coefficientOfVariation = avgY > 0 ? stdDev / avgY : 0;
        
        if (coefficientOfVariation > 0.3) // High variance
            return SpeedTrend.Volatile;
        
        if (slope > avgY * 0.05) // 5% positive slope
            return SpeedTrend.Increasing;
        
        if (slope < -avgY * 0.05) // 5% negative slope
            return SpeedTrend.Decreasing;
        
        return SpeedTrend.Stable;
    }
}
