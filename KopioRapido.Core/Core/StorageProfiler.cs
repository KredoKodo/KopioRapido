using System.Diagnostics;
using System.Runtime.InteropServices;
using KopioRapido.Models;
using Microsoft.Win32.SafeHandles;
#if WINDOWS
using System.Management;
#endif

namespace KopioRapido.Core;

public class StorageProfiler
{
#if WINDOWS
    // P/Invoke declarations for Windows storage detection
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref STORAGE_PROPERTY_QUERY lpInBuffer,
        uint nInBufferSize,
        ref DEVICE_SEEK_PENALTY_DESCRIPTOR lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    // Constants for Windows storage queries
    private const uint FILE_READ_ATTRIBUTES = 0x0080;
    private const uint FILE_SHARE_READ = 0x0001;
    private const uint FILE_SHARE_WRITE = 0x0002;
    private const uint OPEN_EXISTING = 3;
    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;

    // Structs for storage property queries
    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public uint PropertyId;
        public uint QueryType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.U1)]
        public bool IncursSeekPenalty;
    }
#endif

#if MACCATALYST
    // P/Invoke declarations for macOS statfs
    private const int MNT_LOCAL = 0x00001000; // Filesystem is local
    private const int MFSTYPENAMELEN = 16;
    private const int MAXPATHLEN = 1024;

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct StatFs
    {
        public uint f_bsize;      // fundamental file system block size
        public int f_iosize;      // optimal transfer block size
        public ulong f_blocks;    // total data blocks in file system
        public ulong f_bfree;     // free blocks in fs
        public ulong f_bavail;    // free blocks avail to non-superuser
        public ulong f_files;     // total file nodes in file system
        public ulong f_ffree;     // free file nodes in fs
        public fixed int f_fsid[2];  // file system id
        public uint f_owner;      // user that mounted the filesystem
        public uint f_type;       // type of filesystem
        public uint f_flags;      // copy of mount exported flags
        public uint f_fssubtype;  // fs sub-type (flavor)
        public fixed byte f_fstypename[MFSTYPENAMELEN]; // fs type name
        public fixed byte f_mntonname[MAXPATHLEN];      // directory on which mounted
        public fixed byte f_mntfromname[MAXPATHLEN];    // mounted filesystem
        public uint f_reserved_1; // reserved for future use
        public fixed uint f_reserved_2[4]; // reserved for future use
    }

    [DllImport("libc", EntryPoint = "statfs", SetLastError = true)]
    private static extern unsafe int native_statfs(string path, StatFs* buf);
#endif

    private const int BenchmarkSizeMB = 10;
    
    public async Task<StorageProfile> ProfileStorageAsync(string path, CancellationToken cancellationToken = default)
    {
        var profile = new StorageProfile
        {
            Path = path,
            ProfiledAt = DateTime.UtcNow
        };
        
        // Detect storage type (initial detection)
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
            
            // Heuristic: If path is under /Volumes/ and speed is very slow (< 50 MB/s), 
            // it's likely a network share that we failed to detect properly
            // Note: On MacCatalyst, RuntimeInformation.IsOSPlatform(OSPlatform.OSX) returns false!
            if (path.StartsWith("/Volumes/", StringComparison.OrdinalIgnoreCase) &&
                profile.Type != StorageType.NetworkShare &&
                metrics.writeMBps < 50)
            {
                profile.Type = StorageType.NetworkShare;
                profile.IsRemote = true;
            }
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
            // First check DriveInfo.DriveType for network drives
            try
            {
                var root = Path.GetPathRoot(path);
                if (!string.IsNullOrEmpty(root))
                {
                    var driveInfo = new DriveInfo(root);
                    if (driveInfo.DriveType == DriveType.Network)
                        return StorageType.NetworkShare;
                }
            }
            catch { }
            
            // Check filesystem type for network protocols
            var fsType = GetFileSystemType(path);
            
            if (fsType.Contains("nfs", StringComparison.OrdinalIgnoreCase) || 
                fsType.Contains("smb", StringComparison.OrdinalIgnoreCase) || 
                fsType.Contains("cifs", StringComparison.OrdinalIgnoreCase) ||
                fsType.Contains("afp", StringComparison.OrdinalIgnoreCase) ||
                fsType.Contains("webdav", StringComparison.OrdinalIgnoreCase))
            {
                return StorageType.NetworkShare;
            }
            
            // Check if APFS (typically SSD) or HFS+ (could be HDD)
            if (fsType.Contains("apfs", StringComparison.OrdinalIgnoreCase))
                return StorageType.LocalSSD;
            else if (fsType.Contains("hfs", StringComparison.OrdinalIgnoreCase))
                return StorageType.LocalHDD;
            
            // Check if external drive under /Volumes/
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
        System.Diagnostics.Debug.WriteLine($"[StorageProfiler] ===== IsNetworkPath checking: {path} =====");
        
        // UNC paths (Windows/cross-platform)
        if (path.StartsWith("\\\\") || path.StartsWith("//"))
        {
            System.Diagnostics.Debug.WriteLine($"[StorageProfiler] Detected UNC path");
            return true;
        }
        
#if MACCATALYST
        // macOS: Use statfs to properly detect network mounts
        // Note: No need to check RuntimeInformation.IsOSPlatform(OSPlatform.OSX) 
        // because we're already in MACCATALYST conditional compilation
        System.Diagnostics.Debug.WriteLine($"[StorageProfiler] Running on macOS, calling IsNetworkMountMacOS...");
        if (IsNetworkMountMacOS(path))
        {
            System.Diagnostics.Debug.WriteLine($"[StorageProfiler] ✅ statfs detected network mount for: {path}");
            return true;
        }
        System.Diagnostics.Debug.WriteLine($"[StorageProfiler] ❌ statfs did NOT detect network mount");
#endif
        
        try
        {
            // Use DriveInfo.DriveType to detect network drives
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                var driveInfo = new DriveInfo(root);
                
                // Debug logging
                System.Diagnostics.Debug.WriteLine($"[StorageProfiler] DriveInfo check:");
                System.Diagnostics.Debug.WriteLine($"[StorageProfiler]   Path: {path}");
                System.Diagnostics.Debug.WriteLine($"[StorageProfiler]   Root: {root}");
                System.Diagnostics.Debug.WriteLine($"[StorageProfiler]   DriveType: {driveInfo.DriveType}");
                System.Diagnostics.Debug.WriteLine($"[StorageProfiler]   DriveFormat: {driveInfo.DriveFormat}");
                
                if (driveInfo.DriveType == DriveType.Network)
                {
                    System.Diagnostics.Debug.WriteLine($"[StorageProfiler] ✅ DriveInfo detected network");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StorageProfiler] DriveInfo exception: {ex.Message}");
            // Fall through to filesystem type check
        }
        
        // Fallback: Check filesystem type for network protocols (on any macOS-based system)
#if MACCATALYST
        if (path.StartsWith("/Volumes/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/"))
        {
            var fsType = GetFileSystemType(path);
            System.Diagnostics.Debug.WriteLine($"[StorageProfiler] Filesystem type: {fsType}");
            
            if (fsType.Contains("nfs", StringComparison.OrdinalIgnoreCase) || 
                fsType.Contains("smb", StringComparison.OrdinalIgnoreCase) || 
                fsType.Contains("cifs", StringComparison.OrdinalIgnoreCase) ||
                fsType.Contains("afp", StringComparison.OrdinalIgnoreCase) ||
                fsType.Contains("webdav", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine($"[StorageProfiler] Detected as network share by filesystem type");
                return true;
            }
        }
#endif
        
        System.Diagnostics.Debug.WriteLine($"[StorageProfiler] NOT detected as network share");
        return false;
    }
    
    private string GetFileSystemType(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path) ?? path;
            var driveInfo = new DriveInfo(root);
            var format = driveInfo.DriveFormat;
            System.Diagnostics.Debug.WriteLine($"[StorageProfiler] GetFileSystemType({path}) -> Root: {root}, Format: {format}");
            return format;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StorageProfiler] GetFileSystemType exception: {ex.Message}");
            return "Unknown";
        }
    }
    
    private bool IsSSD(string root)
    {
#if WINDOWS
        try
        {
            // Convert path like "C:\" to device handle format "\\.\C:"
            var driveLetter = root.TrimEnd('\\').TrimEnd(':');
            var devicePath = $"\\\\.\\{driveLetter}:";

            // Open device handle
            using var handle = CreateFile(
                devicePath,
                FILE_READ_ATTRIBUTES,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                // Failed to open device, assume SSD (conservative fallback)
                return true;
            }

            // Query for seek penalty property
            var query = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = 7, // StorageDeviceSeekPenaltyProperty
                QueryType = 0,  // PropertyStandardQuery
                AdditionalParameters = new byte[1]
            };

            var descriptor = new DEVICE_SEEK_PENALTY_DESCRIPTOR
            {
                Version = 0,
                Size = (uint)Marshal.SizeOf<DEVICE_SEEK_PENALTY_DESCRIPTOR>()
            };

            bool result = DeviceIoControl(
                handle,
                IOCTL_STORAGE_QUERY_PROPERTY,
                ref query,
                (uint)Marshal.SizeOf<STORAGE_PROPERTY_QUERY>(),
                ref descriptor,
                (uint)Marshal.SizeOf<DEVICE_SEEK_PENALTY_DESCRIPTOR>(),
                out _,
                IntPtr.Zero);

            if (!result)
            {
                // Query failed, assume SSD (conservative fallback)
                return true;
            }

            // No seek penalty = SSD, Seek penalty = HDD
            return !descriptor.IncursSeekPenalty;
        }
        catch
        {
            // Any error, assume SSD (conservative fallback)
            return true;
        }
#elif MACCATALYST
        try
        {
            // Get device path from mount point
            var devicePath = ExecuteCommand("df", $"-h {root}");
            if (string.IsNullOrEmpty(devicePath))
            {
                return true; // Fallback to SSD assumption
            }

            // Extract device name (e.g., /dev/disk1s1)
            var lines = devicePath.Split('\n');
            if (lines.Length < 2)
            {
                return true; // Fallback
            }

            var device = lines[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];

            // Query diskutil for device info
            var diskInfo = ExecuteCommand("diskutil", $"info {device}");

            // Check for solid state indicators
            if (diskInfo.Contains("Solid State: Yes", StringComparison.OrdinalIgnoreCase) ||
                diskInfo.Contains("Medium Type: Solid State", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (diskInfo.Contains("Solid State: No", StringComparison.OrdinalIgnoreCase) ||
                diskInfo.Contains("Medium Type: Rotational", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Fallback: check filesystem type (APFS typically SSD)
            var fsType = GetFileSystemType(root);
            if (fsType.Contains("apfs", StringComparison.OrdinalIgnoreCase))
            {
                return true; // APFS is typically on SSDs
            }

            // Unknown, assume SSD
            return true;
        }
        catch
        {
            // Command execution failed, assume SSD (conservative fallback)
            return true;
        }
#else
        return true; // Fallback for unsupported platforms
#endif
    }
    
    private StorageType DetectUSBSpeed(string root)
    {
#if WINDOWS
        try
        {
            // Get drive letter (e.g., "E" from "E:\")
            var driveLetter = root.TrimEnd('\\').TrimEnd(':');

            // Query WMI for USB devices and their speeds
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");

            foreach (ManagementObject drive in searcher.Get())
            {
                // Get the device ID
                var deviceId = drive["DeviceID"]?.ToString();
                if (string.IsNullOrEmpty(deviceId))
                    continue;

                // Query partitions associated with this drive
                using var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceId.Replace("\\", "\\\\")}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                foreach (ManagementObject partition in partitionSearcher.Get())
                {
                    var partitionDeviceId = partition["DeviceID"]?.ToString();
                    if (string.IsNullOrEmpty(partitionDeviceId))
                        continue;

                    // Query logical disks associated with this partition
                    using var logicalSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionDeviceId.Replace("\\", "\\\\")}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

                    foreach (ManagementObject logical in logicalSearcher.Get())
                    {
                        var logicalName = logical["Name"]?.ToString();
                        if (logicalName?.StartsWith(driveLetter, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            // Found matching drive, check USB version from device name
                            var deviceName = drive["Caption"]?.ToString() ?? string.Empty;
                            var pnpDeviceId = drive["PNPDeviceID"]?.ToString() ?? string.Empty;

                            // USB 3.0 devices typically have "USB\\VID_" with higher speed indicators
                            // Check for USB 3.0 indicators in the PNP ID
                            if (pnpDeviceId.Contains("USB\\VID_", StringComparison.OrdinalIgnoreCase))
                            {
                                // Try to query speed via registry or USB controller
                                // For simplicity, check device capabilities
                                // USB 2.0 max speed: 480 Mbps, USB 3.0+: 5000+ Mbps

                                // Query USB controller for speed capability
                                try
                                {
                                    using var usbSearcher = new ManagementObjectSearcher(
                                        $"SELECT * FROM Win32_USBController");

                                    foreach (ManagementObject usb in usbSearcher.Get())
                                    {
                                        var usbDeviceId = usb["DeviceID"]?.ToString() ?? string.Empty;

                                        // Check if this is a USB 3.0 controller
                                        if (usbDeviceId.Contains("XHCI", StringComparison.OrdinalIgnoreCase) ||
                                            usbDeviceId.Contains("USB3", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // USB 3.0 or newer (xHCI controller)
                                            return StorageType.ExternalUSB3;
                                        }
                                        else if (usbDeviceId.Contains("EHCI", StringComparison.OrdinalIgnoreCase) ||
                                                 usbDeviceId.Contains("USB2", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // USB 2.0 (EHCI controller)
                                            return StorageType.ExternalUSB2;
                                        }
                                    }
                                }
                                catch
                                {
                                    // USB controller query failed, continue with fallback
                                }
                            }

                            // Fallback: check if device name contains version hints
                            if (deviceName.Contains("USB 3", StringComparison.OrdinalIgnoreCase) ||
                                deviceName.Contains("SuperSpeed", StringComparison.OrdinalIgnoreCase))
                            {
                                return StorageType.ExternalUSB3;
                            }

                            if (deviceName.Contains("USB 2", StringComparison.OrdinalIgnoreCase))
                            {
                                return StorageType.ExternalUSB2;
                            }

                            // Default to USB 3.0 if connected to system
                            return StorageType.ExternalUSB3;
                        }
                    }
                }
            }

            // Drive not found in WMI, assume USB 3.0 (optimistic fallback)
            return StorageType.ExternalUSB3;
        }
        catch
        {
            // WMI query failed, assume USB 3.0 (optimistic fallback)
            return StorageType.ExternalUSB3;
        }
#elif MACCATALYST
        try
        {
            // Get device path from mount point
            var devicePath = ExecuteCommand("df", $"-h {root}");
            if (string.IsNullOrEmpty(devicePath))
            {
                return StorageType.ExternalUSB3; // Fallback
            }

            // Extract device name (e.g., /dev/disk2s1)
            var lines = devicePath.Split('\n');
            if (lines.Length < 2)
            {
                return StorageType.ExternalUSB3; // Fallback
            }

            var device = lines[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];

            // Query diskutil for protocol information
            var diskInfo = ExecuteCommand("diskutil", $"info {device}");

            // Check for USB protocol and speed
            if (diskInfo.Contains("Protocol:", StringComparison.OrdinalIgnoreCase))
            {
                // Look for USB speed indicators
                if (diskInfo.Contains("USB 3", StringComparison.OrdinalIgnoreCase) ||
                    diskInfo.Contains("SuperSpeed", StringComparison.OrdinalIgnoreCase) ||
                    diskInfo.Contains("5.0 Gb/s", StringComparison.OrdinalIgnoreCase) ||
                    diskInfo.Contains("10 Gb/s", StringComparison.OrdinalIgnoreCase))
                {
                    return StorageType.ExternalUSB3;
                }

                if (diskInfo.Contains("USB 2", StringComparison.OrdinalIgnoreCase) ||
                    diskInfo.Contains("480 Mb/s", StringComparison.OrdinalIgnoreCase) ||
                    diskInfo.Contains("High-Speed", StringComparison.OrdinalIgnoreCase))
                {
                    return StorageType.ExternalUSB2;
                }
            }

            // Alternative: use system_profiler for more detailed USB info
            var usbInfo = ExecuteCommand("system_profiler", "SPUSBDataType");

            // Search for the device in USB tree
            if (!string.IsNullOrEmpty(usbInfo))
            {
                // Extract disk identifier without partition (e.g., disk2 from disk2s1)
                var diskNumber = System.Text.RegularExpressions.Regex.Match(device, @"disk(\d+)").Value;

                if (usbInfo.Contains(diskNumber, StringComparison.OrdinalIgnoreCase))
                {
                    // Check speed in the vicinity of the disk reference
                    var diskIndex = usbInfo.IndexOf(diskNumber, StringComparison.OrdinalIgnoreCase);
                    var contextStart = Math.Max(0, diskIndex - 500);
                    var contextEnd = Math.Min(usbInfo.Length, diskIndex + 500);
                    var context = usbInfo.Substring(contextStart, contextEnd - contextStart);

                    if (context.Contains("Speed: Up to 5 Gb/s", StringComparison.OrdinalIgnoreCase) ||
                        context.Contains("Speed: Up to 10 Gb/s", StringComparison.OrdinalIgnoreCase))
                    {
                        return StorageType.ExternalUSB3;
                    }

                    if (context.Contains("Speed: Up to 480 Mb/s", StringComparison.OrdinalIgnoreCase))
                    {
                        return StorageType.ExternalUSB2;
                    }
                }
            }

            // Unknown, assume USB 3.0 (optimistic fallback)
            return StorageType.ExternalUSB3;
        }
        catch
        {
            // Command execution failed, assume USB 3.0 (optimistic fallback)
            return StorageType.ExternalUSB3;
        }
#else
        return StorageType.ExternalUSB3; // Fallback for unsupported platforms
#endif
    }

#if MACCATALYST
    private string ExecuteCommand(string command, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000); // 5 second timeout

            return output;
        }
        catch
        {
            return string.Empty;
        }
    }
#endif
    
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
                var latencyBuffer = new byte[4096];
                await fs.ReadExactlyAsync(latencyBuffer, cancellationToken);
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

#if MACCATALYST
    private unsafe bool IsNetworkMountMacOS(string path)
    {
        try
        {
            StatFs buf;
            int result = native_statfs(path, &buf);
            
            if (result != 0)
            {
                System.Diagnostics.Debug.WriteLine($"[StorageProfiler] statfs failed for {path}, errno: {Marshal.GetLastWin32Error()}");
                return false;
            }
            
            // Extract filesystem type name
            string fsTypeName = Marshal.PtrToStringAnsi(new IntPtr(buf.f_fstypename)) ?? "";
            
            // Check if MNT_LOCAL flag is NOT set (meaning it's remote/network)
            bool isNotLocal = (buf.f_flags & MNT_LOCAL) == 0;
            
            // Check if filesystem type is a known network protocol
            bool isNetworkFS = fsTypeName.Equals("smbfs", StringComparison.OrdinalIgnoreCase) ||
                               fsTypeName.Equals("nfs", StringComparison.OrdinalIgnoreCase) ||
                               fsTypeName.Equals("afpfs", StringComparison.OrdinalIgnoreCase) ||
                               fsTypeName.Equals("webdav", StringComparison.OrdinalIgnoreCase) ||
                               fsTypeName.Equals("cifs", StringComparison.OrdinalIgnoreCase);
            
            System.Diagnostics.Debug.WriteLine($"[StorageProfiler] statfs for {path}:");
            System.Diagnostics.Debug.WriteLine($"  f_fstypename: {fsTypeName}");
            System.Diagnostics.Debug.WriteLine($"  f_flags: 0x{buf.f_flags:X}");
            System.Diagnostics.Debug.WriteLine($"  MNT_LOCAL set: {!isNotLocal}");
            System.Diagnostics.Debug.WriteLine($"  Is network FS: {isNetworkFS}");
            System.Diagnostics.Debug.WriteLine($"  Result: {isNotLocal || isNetworkFS}");
            
            return isNotLocal || isNetworkFS;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StorageProfiler] IsNetworkMountMacOS exception: {ex.Message}");
            return false;
        }
    }
#endif
}
