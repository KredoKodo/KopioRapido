using KopioRapido.Models;

namespace KopioRapido.Core;

public class FileComparisonHelper
{
    public async Task<FileComparisonResult> CompareDirectoriesAsync(
        string sourcePath,
        string destPath,
        CancellationToken cancellationToken = default)
    {
        var result = new FileComparisonResult();

        // Get all files from source
        var sourceFiles = Directory.Exists(sourcePath)
            ? Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();

        // Build dictionary of destination files by relative path
        var destFilesByRelativePath = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(destPath))
        {
            var destFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories);
            foreach (var destFile in destFiles)
            {
                var relativePath = Path.GetRelativePath(destPath, destFile);
                destFilesByRelativePath[relativePath] = new FileInfo(destFile);
            }
        }

        // Compare source files
        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourcePath, sourceFile);
            var sourceInfo = new FileInfo(sourceFile);

            if (destFilesByRelativePath.TryGetValue(relativePath, out var destInfo))
            {
                // File exists in both locations - compare
                if (AreFilesIdentical(sourceInfo, destInfo))
                {
                    result.IdenticalFiles.Add(new FilePair
                    {
                        SourcePath = sourceFile,
                        DestPath = destInfo.FullName,
                        RelativePath = relativePath
                    });
                }
                else if (sourceInfo.LastWriteTimeUtc > destInfo.LastWriteTimeUtc)
                {
                    // Source is newer
                    result.SourceNewerFiles.Add(new FilePair
                    {
                        SourcePath = sourceFile,
                        DestPath = destInfo.FullName,
                        RelativePath = relativePath
                    });
                }
                else if (sourceInfo.LastWriteTimeUtc < destInfo.LastWriteTimeUtc)
                {
                    // Destination is newer
                    result.DestNewerFiles.Add(new FilePair
                    {
                        SourcePath = sourceFile,
                        DestPath = destInfo.FullName,
                        RelativePath = relativePath
                    });
                }
                else
                {
                    // Same timestamp but different size - conflict
                    result.ConflictingFiles.Add(new FilePair
                    {
                        SourcePath = sourceFile,
                        DestPath = destInfo.FullName,
                        RelativePath = relativePath
                    });
                }

                // Mark as processed
                destFilesByRelativePath.Remove(relativePath);
            }
            else
            {
                // File only exists in source
                result.SourceOnlyFiles.Add(sourceFile);
            }
        }

        // Remaining files only exist in destination
        result.DestOnlyFiles.AddRange(destFilesByRelativePath.Values.Select(f => f.FullName));

        return await Task.FromResult(result);
    }

    public SyncPlan BuildSyncPlan(
        string sourcePath,
        string destPath,
        FileComparisonResult comparison,
        CopyOperationType operationType)
    {
        var plan = new SyncPlan
        {
            SourcePath = sourcePath,
            DestinationPath = destPath,
            OperationType = operationType
        };

        switch (operationType)
        {
            case CopyOperationType.Copy:
                // Copy all files (no comparison needed)
                plan.FilesToCopy.AddRange(comparison.SourceOnlyFiles);
                plan.FilesToCopy.AddRange(comparison.SourceNewerFiles.Select(f => f.SourcePath));
                plan.FilesToCopy.AddRange(comparison.IdenticalFiles.Select(f => f.SourcePath));
                plan.FilesToCopy.AddRange(comparison.DestNewerFiles.Select(f => f.SourcePath));
                plan.FilesToCopy.AddRange(comparison.ConflictingFiles.Select(f => f.SourcePath));
                break;

            case CopyOperationType.Move:
                // Same as Copy for the copy phase
                plan.FilesToCopy.AddRange(comparison.SourceOnlyFiles);
                plan.FilesToCopy.AddRange(comparison.SourceNewerFiles.Select(f => f.SourcePath));
                plan.FilesToCopy.AddRange(comparison.IdenticalFiles.Select(f => f.SourcePath));
                plan.FilesToCopy.AddRange(comparison.DestNewerFiles.Select(f => f.SourcePath));
                plan.FilesToCopy.AddRange(comparison.ConflictingFiles.Select(f => f.SourcePath));
                // Deletion handled after copy completes
                break;

            case CopyOperationType.Sync:
                // Copy only missing or newer files from source
                plan.FilesToCopy.AddRange(comparison.SourceOnlyFiles);
                plan.FilesToCopy.AddRange(comparison.SourceNewerFiles.Select(f => f.SourcePath));
                // Skip identical and dest-newer files
                plan.IdenticalFilesToSkip.AddRange(comparison.IdenticalFiles.Select(f => f.SourcePath));
                break;

            case CopyOperationType.Mirror:
                // Same as Sync, plus delete extra files in destination
                plan.FilesToCopy.AddRange(comparison.SourceOnlyFiles);
                plan.FilesToCopy.AddRange(comparison.SourceNewerFiles.Select(f => f.SourcePath));
                plan.IdenticalFilesToSkip.AddRange(comparison.IdenticalFiles.Select(f => f.SourcePath));
                plan.FilesToDelete.AddRange(comparison.DestOnlyFiles);
                break;

            case CopyOperationType.BiDirectionalSync:
                // Copy newer files in both directions
                plan.FilesToCopy.AddRange(comparison.SourceOnlyFiles);
                plan.FilesToCopy.AddRange(comparison.SourceNewerFiles.Select(f => f.SourcePath));
                // For dest-newer files, we'll copy from dest to source (handled separately)
                plan.FilesToCopyReverse.AddRange(comparison.DestOnlyFiles);
                plan.FilesToCopyReverse.AddRange(comparison.DestNewerFiles.Select(f => f.DestPath));
                // Conflicts: log and skip
                plan.ConflictsToLog.AddRange(comparison.ConflictingFiles.Select(f => f.RelativePath));
                break;
        }

        // Calculate totals
        plan.TotalFilesToCopy = plan.FilesToCopy.Count + plan.FilesToCopyReverse.Count;
        plan.TotalBytesToCopy = plan.FilesToCopy.Sum(f => new FileInfo(f).Length) +
                                plan.FilesToCopyReverse.Sum(f => new FileInfo(f).Length);
        plan.TotalFilesToDelete = plan.FilesToDelete.Count;

        return plan;
    }

    private bool AreFilesIdentical(FileInfo source, FileInfo dest)
    {
        // Compare by size and timestamp (fast comparison)
        return source.Length == dest.Length &&
               source.LastWriteTimeUtc == dest.LastWriteTimeUtc;
    }
}

public class FileComparisonResult
{
    public List<FilePair> IdenticalFiles { get; set; } = new();
    public List<FilePair> SourceNewerFiles { get; set; } = new();
    public List<FilePair> DestNewerFiles { get; set; } = new();
    public List<FilePair> ConflictingFiles { get; set; } = new();
    public List<string> SourceOnlyFiles { get; set; } = new();
    public List<string> DestOnlyFiles { get; set; } = new();
}

public class FilePair
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}

public class SyncPlan
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public CopyOperationType OperationType { get; set; }
    public List<string> FilesToCopy { get; set; } = new();
    public List<string> FilesToCopyReverse { get; set; } = new();  // For BiDirectionalSync
    public List<string> FilesToDelete { get; set; } = new();       // For Mirror/Move
    public List<string> IdenticalFilesToSkip { get; set; } = new();
    public List<string> ConflictsToLog { get; set; } = new();
    public int TotalFilesToCopy { get; set; }
    public long TotalBytesToCopy { get; set; }
    public int TotalFilesToDelete { get; set; }
}
