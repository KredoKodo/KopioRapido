using System.Text.Json;
using KopioRapido.Models;

namespace KopioRapido.Services;

public class ResumeService : IResumeService
{
    private readonly string _stateDirectory;
    private readonly SemaphoreSlim _fileLock;

    public ResumeService()
    {
        _stateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KopioRapido",
            "Operations"
        );
        Directory.CreateDirectory(_stateDirectory);
        _fileLock = new SemaphoreSlim(1, 1);
    }

    public async Task SaveOperationStateAsync(CopyOperation operation)
    {
        var stateFile = GetStateFilePath(operation.Id);
        var json = JsonSerializer.Serialize(operation, new JsonSerializerOptions { WriteIndented = true });

        await _fileLock.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(stateFile, json);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<CopyOperation?> LoadOperationStateAsync(string operationId)
    {
        var stateFile = GetStateFilePath(operationId);
        if (!File.Exists(stateFile))
        {
            return null;
        }

        await _fileLock.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(stateFile);
            return JsonSerializer.Deserialize<CopyOperation>(json);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IEnumerable<CopyOperation>> GetResumableOperationsAsync()
    {
        var operations = new List<CopyOperation>();

        if (!Directory.Exists(_stateDirectory))
        {
            return operations;
        }

        var stateFiles = Directory.GetFiles(_stateDirectory, "*.json");

        foreach (var file in stateFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var operation = JsonSerializer.Deserialize<CopyOperation>(json);

                if (operation != null &&
                    (operation.Status == CopyStatus.InProgress ||
                     operation.Status == CopyStatus.Paused ||
                     operation.Status == CopyStatus.Failed) &&
                    operation.CanResume)
                {
                    operations.Add(operation);
                }
            }
            catch
            {
                // Skip corrupted state files
            }
        }

        return operations;
    }

    public async Task DeleteOperationStateAsync(string operationId)
    {
        var stateFile = GetStateFilePath(operationId);
        if (File.Exists(stateFile))
        {
            await _fileLock.WaitAsync();
            try
            {
                File.Delete(stateFile);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }

    public async Task<bool> CanResumeAsync(string operationId)
    {
        var operation = await LoadOperationStateAsync(operationId);
        if (operation == null)
        {
            return false;
        }

        if (!operation.CanResume)
        {
            return false;
        }

        if (!Directory.Exists(operation.SourcePath) && !File.Exists(operation.SourcePath))
        {
            return false;
        }

        return true;
    }

    private string GetStateFilePath(string operationId)
    {
        return Path.Combine(_stateDirectory, $"{operationId}.json");
    }
}
