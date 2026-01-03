using KopioRapido.Models;

namespace KopioRapido.Services;

public interface IResumeService
{
    Task SaveOperationStateAsync(CopyOperation operation);
    Task<CopyOperation?> LoadOperationStateAsync(string operationId);
    Task<IEnumerable<CopyOperation>> GetResumableOperationsAsync();
    Task DeleteOperationStateAsync(string operationId);
    Task<bool> CanResumeAsync(string operationId);
}
