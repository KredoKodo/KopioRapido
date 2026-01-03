using KopioRapido.Models;

namespace KopioRapido.Core;

public class FileAnalyzer
{
    private static readonly HashSet<string> CompressibleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".csv", ".json", ".xml", ".sql", ".htm", ".html", 
        ".css", ".js", ".ts", ".c", ".cpp", ".h", ".cs", ".java", ".py",
        ".md", ".yaml", ".yml", ".ini", ".conf", ".bmp", ".tiff", ".svg"
    };
    
    private static readonly HashSet<string> CompressedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".gz", ".bz2", ".xz", ".tar",
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv",
        ".mp3", ".m4a", ".aac", ".ogg", ".wma", ".flac",
        ".pdf", ".docx", ".xlsx", ".pptx"
    };
    
    public async Task<FileSetProfile> AnalyzeFilesAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var profile = new FileSetProfile();
        
        if (File.Exists(sourcePath))
        {
            // Single file
            var fileInfo = new FileInfo(sourcePath);
            profile.TotalFiles = 1;
            profile.TotalBytes = fileInfo.Length;
            ClassifyFile(fileInfo, profile);
        }
        else if (Directory.Exists(sourcePath))
        {
            // Directory - quick analysis
            await Task.Run(() => AnalyzeDirectory(sourcePath, profile, cancellationToken), cancellationToken);
        }
        
        // Calculate average file size
        if (profile.TotalFiles > 0)
        {
            profile.AverageFileSizeMB = (profile.TotalBytes / (double)profile.TotalFiles) / (1024 * 1024);
        }
        
        return profile;
    }
    
    private void AnalyzeDirectory(string path, FileSetProfile profile, CancellationToken cancellationToken)
    {
        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        
        // For very large file sets, sample to speed up analysis
        var filesToAnalyze = files.Length > 1000 
            ? SampleFiles(files, 1000) 
            : files;
        
        int currentDepth = 0;
        
        foreach (var filePath in filesToAnalyze)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            try
            {
                var fileInfo = new FileInfo(filePath);
                
                profile.TotalBytes += fileInfo.Length;
                ClassifyFile(fileInfo, profile);
                
                // Calculate directory depth
                var relativePath = Path.GetRelativePath(path, filePath);
                var depth = relativePath.Split(Path.DirectorySeparatorChar).Length - 1;
                if (depth > currentDepth)
                    currentDepth = depth;
            }
            catch
            {
                // Skip files we can't access
            }
        }
        
        profile.TotalFiles = files.Length;
        profile.MaxDirectoryDepth = currentDepth;
        
        // If we sampled, extrapolate the classification
        if (files.Length > filesToAnalyze.Length)
        {
            double scaleFactor = (double)files.Length / filesToAnalyze.Length;
            profile.TinyFiles = (int)(profile.TinyFiles * scaleFactor);
            profile.SmallFiles = (int)(profile.SmallFiles * scaleFactor);
            profile.MediumFiles = (int)(profile.MediumFiles * scaleFactor);
            profile.LargeFiles = (int)(profile.LargeFiles * scaleFactor);
            profile.HugeFiles = (int)(profile.HugeFiles * scaleFactor);
            profile.CompressibleFiles = (int)(profile.CompressibleFiles * scaleFactor);
            profile.AlreadyCompressedFiles = (int)(profile.AlreadyCompressedFiles * scaleFactor);
        }
    }
    
    private void ClassifyFile(FileInfo fileInfo, FileSetProfile profile)
    {
        var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
        
        // Size classification
        if (sizeMB < 1)
            profile.TinyFiles++;
        else if (sizeMB < 10)
            profile.SmallFiles++;
        else if (sizeMB < 100)
            profile.MediumFiles++;
        else if (sizeMB < 1024)
            profile.LargeFiles++;
        else
            profile.HugeFiles++;
        
        // Compression classification
        var extension = fileInfo.Extension;
        if (CompressibleExtensions.Contains(extension))
        {
            profile.CompressibleFiles++;
        }
        else if (CompressedExtensions.Contains(extension))
        {
            profile.AlreadyCompressedFiles++;
        }
        
        // File type distribution
        if (!string.IsNullOrEmpty(extension))
        {
            if (profile.FileTypeDistribution.ContainsKey(extension))
                profile.FileTypeDistribution[extension]++;
            else
                profile.FileTypeDistribution[extension] = 1;
        }
    }
    
    private string[] SampleFiles(string[] files, int sampleSize)
    {
        if (files.Length <= sampleSize)
            return files;
        
        var random = new Random();
        var sampled = new string[sampleSize];
        var indices = new HashSet<int>();
        
        // Include first and last files
        sampled[0] = files[0];
        sampled[1] = files[files.Length - 1];
        indices.Add(0);
        indices.Add(files.Length - 1);
        
        // Random sampling for the rest
        for (int i = 2; i < sampleSize; i++)
        {
            int index;
            do
            {
                index = random.Next(files.Length);
            } while (indices.Contains(index));
            
            indices.Add(index);
            sampled[i] = files[index];
        }
        
        return sampled;
    }
}
