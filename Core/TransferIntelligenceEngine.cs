using KopioRapido.Models;

namespace KopioRapido.Core;

public class TransferIntelligenceEngine
{
    private readonly StorageProfiler _storageProfiler;
    private readonly FileAnalyzer _fileAnalyzer;
    
    public TransferIntelligenceEngine(StorageProfiler storageProfiler, FileAnalyzer fileAnalyzer)
    {
        _storageProfiler = storageProfiler;
        _fileAnalyzer = fileAnalyzer;
    }
    
    public async Task<(StorageProfile source, StorageProfile destination, FileSetProfile files, TransferStrategy strategy)> 
        AnalyzeAndSelectStrategyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        // Profile both storage locations in parallel
        var sourceTask = _storageProfiler.ProfileStorageAsync(sourcePath, cancellationToken);
        var destTask = _storageProfiler.ProfileStorageAsync(destinationPath, cancellationToken);
        var filesTask = _fileAnalyzer.AnalyzeFilesAsync(sourcePath, cancellationToken);
        
        await Task.WhenAll(sourceTask, destTask, filesTask);
        
        var sourceProfile = await sourceTask;
        var destProfile = await destTask;
        var fileProfile = await filesTask;
        
        // Select optimal strategy
        var strategy = SelectStrategy(sourceProfile, destProfile, fileProfile);
        
        return (sourceProfile, destProfile, fileProfile, strategy);
    }
    
    private TransferStrategy SelectStrategy(StorageProfile source, StorageProfile dest, FileSetProfile files)
    {
        // Decision tree based on storage types and file characteristics
        TransferStrategy strategy;
        
        // Rule 1: Network transfers with many files benefit from parallelism
        if ((source.IsRemote || dest.IsRemote) && files.TotalFiles > 50)
        {
            strategy = files.TotalFiles switch
            {
                > 500 => TransferStrategy.ParallelAggressive(
                    $"Network transfer with {files.TotalFiles} files - high parallelism masks latency"),
                > 200 => TransferStrategy.ParallelModerate(
                    $"Network transfer with {files.TotalFiles} files - moderate parallelism"),
                _ => TransferStrategy.ParallelConservative(
                    $"Network transfer with {files.TotalFiles} files - conservative parallelism")
            };
        }
        // Rule 2: HDD destinations should avoid parallel writes (causes seeking)
        else if (dest.Type == StorageType.LocalHDD)
        {
            strategy = TransferStrategy.Sequential(
                "Hard drive destination - sequential to avoid seek penalties");
        }
        // Rule 3: Many small files benefit from parallel even on local storage
        else if (files.TotalFiles > 100 && files.TinyFiles + files.SmallFiles > files.TotalFiles * 0.7)
        {
            if (source.SupportsParallelIO && dest.SupportsParallelIO)
            {
                strategy = TransferStrategy.ParallelAggressive(
                    $"{files.TotalFiles} small files - parallel reduces overhead");
            }
            else
            {
                strategy = TransferStrategy.Sequential("Default safe mode");
            }
        }
        // Rule 4: SSD to SSD with moderate file count
        else if (source.Type == StorageType.LocalSSD && dest.Type == StorageType.LocalSSD)
        {
            if (files.TotalFiles > 20)
            {
                strategy = TransferStrategy.ParallelModerate(
                    "Fast SSD storage - moderate parallelism for efficiency");
            }
            else
            {
                strategy = TransferStrategy.Sequential(
                    "Few large files on SSD - sequential is fastest");
            }
        }
        // Rule 5: Large files (already fast sequential)
        else if (files.HugeFiles > files.TotalFiles / 2)
        {
            strategy = TransferStrategy.Sequential(
                "Large files already saturate bandwidth - sequential mode");
        }
        // Rule 6: USB 2.0 or slow storage - keep it simple
        else if (source.Type == StorageType.ExternalUSB2 || dest.Type == StorageType.ExternalUSB2)
        {
            strategy = TransferStrategy.Sequential(
                "USB 2.0 detected - sequential for best compatibility");
        }
        // Rule 7: Mixed files on modern storage
        else if (files.TotalFiles > 10 && source.SupportsParallelIO && dest.SupportsParallelIO)
        {
            strategy = TransferStrategy.ParallelConservative(
                "Modern storage - conservative parallel for balanced performance");
        }
        // Default: Sequential (safe fallback)
        else
        {
            strategy = TransferStrategy.Sequential(
                "Default safe mode - sequential transfer");
        }
        
        // Compression decision: Enable for network transfers with compressible files
        if ((source.IsRemote || dest.IsRemote) && ShouldUseCompression(source, dest, files))
        {
            strategy.UseCompression = true;
            
            // Calculate actual compressible ratio for messaging
            double compressibleRatio = (double)files.CompressibleFiles / files.TotalFiles;
            
            if (compressibleRatio > 0.5)
            {
                // Majority are compressible - highlight compression
                int estimatedSpeedup = compressibleRatio > 0.7 ? 3 : 2;
                strategy.Reasoning += $" + compression ({estimatedSpeedup}x faster for {files.CompressibleFiles} compressible files)";
                strategy.UserFriendlyDescription += " + compression";
            }
            else if (files.CompressibleFiles > 0)
            {
                // Minority are compressible - mention selective compression
                strategy.Reasoning += $" + selective compression for {files.CompressibleFiles} text/code files";
            }
        }
        
        return strategy;
    }
    
    private bool ShouldUseCompression(StorageProfile source, StorageProfile dest, FileSetProfile files)
    {
        // Enable selective compression if we're on a network and have ANY compressible files
        // We'll compress on a per-file basis rather than all-or-nothing
        
        // Check if network is slow enough that compression helps
        bool slowNetwork = (source.IsRemote && source.SequentialWriteMBps < 100) ||
                          (dest.IsRemote && dest.SequentialWriteMBps < 100);
        
        if (!slowNetwork)
            return false;
        
        // Enable if we have at least SOME compressible files (even if it's a small percentage)
        // We'll selectively compress only those files
        return files.CompressibleFiles > 0;
    }
    
    public string GenerateUserFriendlyMessage(StorageProfile source, StorageProfile dest, FileSetProfile files, TransferStrategy strategy)
    {
        var speedBoost = EstimateSpeedBoost(source, dest, files, strategy);
        var emoji = GetStrategyEmoji(strategy);
        
        var message = $"{emoji} {strategy.UserFriendlyDescription}";
        
        if (speedBoost > 1.5)
        {
            message += $" ({speedBoost:F1}x faster)";
        }
        
        return message;
    }
    
    private double EstimateSpeedBoost(StorageProfile source, StorageProfile dest, FileSetProfile files, TransferStrategy strategy)
    {
        // Rough estimates of performance improvement
        double boost = 1.0;
        
        if (strategy.Mode != TransferMode.Sequential)
        {
            // Network transfers benefit the most from parallelism
            if (source.IsRemote || dest.IsRemote)
            {
                boost = strategy.MaxConcurrentFiles switch
                {
                    >= 16 => 4.0,
                    >= 8 => 3.0,
                    >= 4 => 2.0,
                    _ => 1.0
                };
            }
            // Many small files benefit from parallelism
            else if (files.TinyFiles + files.SmallFiles > files.TotalFiles * 0.7)
            {
                boost = strategy.MaxConcurrentFiles switch
                {
                    >= 16 => 3.0,
                    >= 8 => 2.5,
                    >= 4 => 1.8,
                    _ => 1.0
                };
            }
            // SSD parallel benefits
            else if (source.Type == StorageType.LocalSSD && dest.Type == StorageType.LocalSSD)
            {
                boost = strategy.MaxConcurrentFiles switch
                {
                    >= 8 => 2.0,
                    >= 4 => 1.5,
                    _ => 1.0
                };
            }
            else
            {
                boost = 1.2; // Modest improvement
            }
        }
        
        // Add compression boost
        if (strategy.UseCompression)
        {
            double compressibleRatio = (double)files.CompressibleFiles / files.TotalFiles;
            double compressionBoost = compressibleRatio > 0.7 ? 2.5 : compressibleRatio > 0.5 ? 1.8 : 1.3;
            boost *= compressionBoost;
        }
        
        return boost;
    }
    
    private string GetStrategyEmoji(TransferStrategy strategy)
    {
        // Add compression emoji if enabled
        if (strategy.UseCompression)
        {
            return strategy.Mode switch
            {
                TransferMode.ParallelAggressive => "🚀🗜️",
                TransferMode.ParallelModerate => "⚡🗜️",
                TransferMode.ParallelConservative => "💨🗜️",
                _ => "📁🗜️"
            };
        }
        
        return strategy.Mode switch
        {
            TransferMode.ParallelAggressive => "🚀",
            TransferMode.ParallelModerate => "⚡",
            TransferMode.ParallelConservative => "💨",
            _ => "📁"
        };
    }
}
