using System.Diagnostics;
using System.Runtime.InteropServices;
using KopioRapido.Models;

namespace KopioRapido.Core;

public class StorageProfiler
{
    private const int BenchmarkSizeMB = 10;
    
    public async Task<StorageProfile> ProfileStorageAsync(string path, CancellationToken cancellationToken = default)
    {
        var profile = new StorageProfile
        {
            Path = path,
            ProfiledAt = DateTime.UtcNow
        };
        
        // Detect storage type
        profile.Type = DetectStorageType(path);
        profile.IsRemote = IsNetworkPath(path);
        profile.FileSystemType = GetFileSystemType(path);
        
        // Quick benchmark
        try
        {
            var metrics = await QuickBenchmarkAsync(path, cancellationToken);
            profile.SequentialWriteMBps = metrics.writeMBps;
            profile.SequentialReadMBps = metrics.readMBps;
            profile.LatencyMs = metrics.latencyMs;
        }
        catch
        {
            // Benchmark failed, use conservative estimates
            profile.SequentialWriteMBps = 50;
            profile.SequentialReadMBps = 50;
        }
        
        // Determine if parallel I/O is beneficial
        profile.SupportsParallelIO = profile.Type switch
        {
            StorageType.LocalSSD => true,
            StorageType.NetworkShare => true,
            StorageType.ExternalThunderbolt => true,
            StorageType.ExternalUSB3 => true,
            StorageType.LocalHDD => false, // Parallel hurts HDDs
            StorageType.ExternalUSB2 => false,
            _ => true
        };
        
        return profile;
    }
    
    private StorageType DetectStorageType(string path)
    {
        if (IsNetworkPath(path))
            return StorageType.NetworkShare;
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return DetectStorageTypeWindows(path);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return DetectStorageTypeMacOS(path);
        else
            return StorageType.Unknown;
    }
    
    private StorageType DetectStorageTypeWindows(string path)
    {
#if WINDOWS
        try
        {
            var root = Path.GetPathRoot(path) ?? path;
            var driveInfo = new DriveInfo(root);
            
            // Check drive type
            return driveInfo.DriveType switch
            {
                DriveType.Network => StorageType.NetworkShare,
                DriveType.Fixed => IsSSD(root) ? StorageType.LocalSSD : StorageType.LocalHDD,
                DriveType.Removable => DetectUSBSpeed(root),
                _ => StorageType.Unknown
            };
        }
        catch
        {
            return StorageType.Unknown;
        }
#else
        return StorageType.Unknown;
#endif
    }
    
    private StorageType DetectStorageTypeMacOS(string path)
    {
#if MACCATALYST
        try
        {
            // Check if path is on APFS (typically SSD) or HFS+ (could be HDD)
            var fsType = GetFileSystemType(path);
            
            if (fsType.Contains("apfs", StringComparison.OrdinalIgnoreCase))
                return StorageType.LocalSSD;
            else if (fsType.Contains("hfs", StringComparison.OrdinalIgnoreCase))
                return StorageType.LocalHDD;
            else if (fsType.Contains("nfs") || fsType.Contains("smb") || fsType.Contains("afp"))
                return StorageType.NetworkShare;
            
            // Check if external drive
            if (path.StartsWith("/Volumes/", StringComparison.OrdinalIgnoreCase))
            {
                // External drive, assume USB 3.0 for now
                return StorageType.ExternalUSB3;
            }
            
            return StorageType.Unknown;
        }
        catch
        {
            return StorageType.Unknown;
        }
#else
        return StorageType.Unknown;
#endif
    }
    
    private bool IsNetworkPath(string path)
    {
        if (path.StartsWith("\\\\") || path.StartsWith("//"))
            return true;
        
        if (path.StartsWith("/Volumes/", StringComparison.OrdinalIgnoreCase))
        {
            // Could be network mount on macOS
            var fsType = GetFileSystemType(path);
            return fsType.Contains("nfs") || fsType.Contains("smb") || fsType.Contains("afp");
        }
        
        return false;
    }
    
    private string GetFileSystemType(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path) ?? path;
            var driveInfo = new DriveInfo(root);
            return driveInfo.DriveFormat;
        }
        catch
        {
            return "Unknown";
        }
    }
    
    private bool IsSSD(string root)
    {
        // For now, assume modern systems have SSDs
        // TODO: Implement actual SSD detection via DeviceIoControl on Windows
        // TODO: Implement diskutil on macOS
        return true;
    }
    
    private StorageType DetectUSBSpeed(string root)
    {
        // TODO: Detect USB 2.0 vs 3.0 via device queries
        // For now, assume USB 3.0
        return StorageType.ExternalUSB3;
    }
    
    private async Task<(double writeMBps, double readMBps, double latencyMs)> QuickBenchmarkAsync(
        string path, 
        CancellationToken cancellationToken)
    {
        var testFile = Path.Combine(path, $".kopiorapido_bench_{Guid.NewGuid():N}.tmp");
        var buffer = new byte[1024 * 1024]; // 1MB buffer
        new Random().NextBytes(buffer);
        
        try
        {
            // Write test
            var writeSw = Stopwatch.StartNew();
            await using (var fs = new FileStream(testFile, FileMode.Create, FileAccess.Write, FileShare.None, 
                bufferSize: 1024 * 1024, useAsync: true))
            {
                for (int i = 0; i < BenchmarkSizeMB; i++)
                {
                    await fs.WriteAsync(buffer, cancellationToken);
                }
                await fs.FlushAsync(cancellationToken);
            }
            writeSw.Stop();
            var writeMBps = BenchmarkSizeMB / writeSw.Elapsed.TotalSeconds;
            
            // Read test
            var readSw = Stopwatch.StartNew();
            await using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None,
                bufferSize: 1024 * 1024, useAsync: true))
            {
                while (await fs.ReadAsync(buffer, cancellationToken) > 0) { }
            }
            readSw.Stop();
            var readMBps = BenchmarkSizeMB / readSw.Elapsed.TotalSeconds;
            
            // Latency test (small random access)
            var latencySw = Stopwatch.StartNew();
            await using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                fs.Seek(BenchmarkSizeMB * 512 * 1024, SeekOrigin.Begin);
                await fs.ReadAsync(new byte[4096], cancellationToken);
            }
            latencySw.Stop();
            var latencyMs = latencySw.Elapsed.TotalMilliseconds;
            
            return (writeMBps, readMBps, latencyMs);
        }
        finally
        {
            try
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
            catch { }
        }
    }
}
