using KopioRapido.Models;

namespace KopioRapido.Services;

public interface IQuickScanService
{
    /// <summary>
    /// Quickly scan directory for summary stats without creating full FileItem objects
    /// Target: < 100ms for any file count
    /// </summary>
    Task<FileScanSummary> QuickScanAsync(string sourcePath, int previewCount = 20, CancellationToken cancellationToken = default);
}
