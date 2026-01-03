using KopioRapido.Core;
using KopioRapido.Models;

namespace KopioRapido.Services;

public class FileOperationService : IFileOperationService
{
    private readonly FileCopyEngine _copyEngine;
    private readonly IResumeService _resumeService;
    private readonly Dictionary<string, CopyOperation> _activeOperations;

    public FileOperationService(
        FileCopyEngine copyEngine,
        IResumeService resumeService)
    {
        _copyEngine = copyEngine;
        _resumeService = resumeService;
        _activeOperations = new Dictionary<string, CopyOperation>();
    }

    public async Task<CopyOperation> StartCopyAsync(
        string source,
        string destination,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = await _copyEngine.CopyAsync(source, destination, CopyOperationType.Copy, progress, cancellationToken);
        _activeOperations[operation.Id] = operation;
        return operation;
    }

    public async Task<CopyOperation> StartMoveAsync(
        string source,
        string destination,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = await _copyEngine.CopyAsync(source, destination, CopyOperationType.Move, progress, cancellationToken);
        _activeOperations[operation.Id] = operation;
        return operation;
    }

    public async Task<CopyOperation> StartMirrorAsync(
        string source,
        string destination,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = await _copyEngine.CopyAsync(source, destination, CopyOperationType.Mirror, progress, cancellationToken);
        _activeOperations[operation.Id] = operation;
        return operation;
    }

    public async Task<VerificationResult> StartVerifyAsync(
        string source,
        string destination,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await _copyEngine.VerifyAsync(source, destination, progress, cancellationToken);
    }

    public async Task<CopyOperation> ResumeCopyAsync(
        string operationId,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Use the new smart resume functionality from FileCopyEngine
        var operation = await _copyEngine.ResumeAsync(operationId, progress, cancellationToken);

        _activeOperations[operation.Id] = operation;
        return operation;
    }

    public Task PauseCopyAsync(string operationId)
    {
        _copyEngine.CancelOperation(operationId);
        return Task.CompletedTask;
    }

    public Task CancelCopyAsync(string operationId)
    {
        _copyEngine.CancelOperation(operationId);
        return _resumeService.DeleteOperationStateAsync(operationId);
    }

    public async Task<CopyOperation?> GetOperationAsync(string operationId)
    {
        if (_activeOperations.TryGetValue(operationId, out var operation))
        {
            return operation;
        }

        return await _resumeService.LoadOperationStateAsync(operationId);
    }

    public async Task<IEnumerable<CopyOperation>> GetPendingOperationsAsync()
    {
        return await _resumeService.GetResumableOperationsAsync();
    }
}
