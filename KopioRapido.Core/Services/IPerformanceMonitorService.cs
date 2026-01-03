using KopioRapido.Models;

namespace KopioRapido.Services;

public interface IPerformanceMonitorService
{
    /// <summary>
    /// Start monitoring performance for an operation
    /// </summary>
    void StartMonitoring(string operationId, int initialConcurrency);
    
    /// <summary>
    /// Stop monitoring an operation
    /// </summary>
    void StopMonitoring(string operationId);
    
    /// <summary>
    /// Record a performance sample
    /// </summary>
    void RecordSample(string operationId, double speedMBps, int activeConcurrency);
    
    /// <summary>
    /// Get current performance metrics
    /// </summary>
    PerformanceMetrics? GetMetrics(string operationId);
    
    /// <summary>
    /// Determine if concurrency should be adjusted
    /// </summary>
    (bool shouldAdjust, int newConcurrency, string reason) ShouldAdjustConcurrency(string operationId);
    
    /// <summary>
    /// Record that an adaptation was applied
    /// </summary>
    void RecordAdaptation(string operationId, int newConcurrency, string reason);
}
