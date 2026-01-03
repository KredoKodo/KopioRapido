namespace KopioRapido.Models;

public enum TransferMode
{
    Sequential,
    ParallelConservative,
    ParallelModerate,
    ParallelAggressive
}

public class TransferStrategy
{
    public TransferMode Mode { get; set; }
    public int MaxConcurrentFiles { get; set; } = 1;
    public int BufferSizeKB { get; set; } = 1024; // 1MB default
    public bool UseCompression { get; set; }
    public bool UseDeltaSync { get; set; } = true;
    public string Reasoning { get; set; } = string.Empty;
    public string UserFriendlyDescription { get; set; } = string.Empty;
    
    // Pre-calculated totals to skip re-scanning
    public int? PreCalculatedTotalFiles { get; set; }
    public long? PreCalculatedTotalBytes { get; set; }
    
    public static TransferStrategy Sequential(string reasoning) => new()
    {
        Mode = TransferMode.Sequential,
        MaxConcurrentFiles = 1,
        BufferSizeKB = 1024,
        Reasoning = reasoning,
        UserFriendlyDescription = "Sequential mode"
    };
    
    public static TransferStrategy ParallelConservative(string reasoning) => new()
    {
        Mode = TransferMode.ParallelConservative,
        MaxConcurrentFiles = 4,
        BufferSizeKB = 512,
        Reasoning = reasoning,
        UserFriendlyDescription = "Parallel mode (4 files)"
    };
    
    public static TransferStrategy ParallelModerate(string reasoning) => new()
    {
        Mode = TransferMode.ParallelModerate,
        MaxConcurrentFiles = 8,
        BufferSizeKB = 512,
        Reasoning = reasoning,
        UserFriendlyDescription = "Parallel mode (8 files)"
    };
    
    public static TransferStrategy ParallelAggressive(string reasoning) => new()
    {
        Mode = TransferMode.ParallelAggressive,
        MaxConcurrentFiles = 16,
        BufferSizeKB = 256,
        Reasoning = reasoning,
        UserFriendlyDescription = "Parallel mode (16 files)"
    };
}
