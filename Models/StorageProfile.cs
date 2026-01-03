namespace KopioRapido.Models;

public enum StorageType
{
    Unknown,
    LocalSSD,
    LocalHDD,
    NetworkShare,
    ExternalUSB2,
    ExternalUSB3,
    ExternalThunderbolt,
    CloudMount
}

public class StorageProfile
{
    public string Path { get; set; } = string.Empty;
    public StorageType Type { get; set; }
    public string FileSystemType { get; set; } = string.Empty;
    public double SequentialReadMBps { get; set; }
    public double SequentialWriteMBps { get; set; }
    public double RandomReadMBps { get; set; }
    public double LatencyMs { get; set; }
    public bool SupportsParallelIO { get; set; }
    public bool IsRemote { get; set; }
    public DateTime ProfiledAt { get; set; }
    
    public string FriendlyName => Type switch
    {
        StorageType.LocalSSD => "Fast Local SSD",
        StorageType.LocalHDD => "Local Hard Drive",
        StorageType.NetworkShare => "Network Share",
        StorageType.ExternalUSB3 => "USB 3.0 Drive",
        StorageType.ExternalUSB2 => "USB 2.0 Drive",
        StorageType.ExternalThunderbolt => "Thunderbolt Drive",
        StorageType.CloudMount => "Cloud Storage",
        _ => "Storage Device"
    };
}
