using KopioRapido.Models;

namespace KopioRapido.Services;

public interface IFileOperationService
{
    // Legacy Copy-specific methods (for backward compatibility)
    Task<CopyOperation> StartCopyAsync(string source, string destination, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default, TransferStrategy? strategy = null);
    Task<CopyOperation> ResumeCopyAsync(string operationId, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    Task PauseCopyAsync(string operationId);
    Task CancelCopyAsync(string operationId);
    Task<CopyOperation?> GetOperationAsync(string operationId);
    Task<IEnumerable<CopyOperation>> GetPendingOperationsAsync();

    // New operation-type-aware methods
    Task<CopyOperation> StartOperationAsync(
        string source,
        string destination,
        CopyOperationType operationType,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        TransferStrategy? strategy = null);

    Task<SyncOperationSummary> AnalyzeSyncAsync(
        string sourcePath,
        string destinationPath,
        CopyOperationType operationType,
        CancellationToken cancellationToken = default);

    // Intelligence engine integration
    Task<(StorageProfile source, StorageProfile destination, FileSetProfile files, TransferStrategy strategy)> AnalyzeAndSelectStrategyAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
    string GenerateStrategyMessage(StorageProfile source, StorageProfile dest, FileSetProfile files, TransferStrategy strategy);
}
