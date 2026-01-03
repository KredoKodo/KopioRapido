using KopioRapido.Models;

namespace KopioRapido.Services;

public interface IFileOperationService
{
    Task<CopyOperation> StartCopyAsync(string source, string destination, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default, TransferStrategy? strategy = null);
    Task<CopyOperation> ResumeCopyAsync(string operationId, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    Task PauseCopyAsync(string operationId);
    Task CancelCopyAsync(string operationId);
    Task<CopyOperation?> GetOperationAsync(string operationId);
    Task<IEnumerable<CopyOperation>> GetPendingOperationsAsync();
    Task<(StorageProfile source, StorageProfile destination, FileSetProfile files, TransferStrategy strategy)> AnalyzeAndSelectStrategyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    string GenerateStrategyMessage(StorageProfile source, StorageProfile dest, FileSetProfile files, TransferStrategy strategy);
}
