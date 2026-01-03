using KopioRapido.Models;

namespace KopioRapido.CLI.Output;

public interface IOutputFormatter
{
    void ShowAnalysis(StorageProfile source, StorageProfile dest,
                     FileSetProfile files, TransferStrategy strategy);
    void ShowSyncSummary(SyncOperationSummary summary);
    void ShowProgress(FileTransferProgress progress);
    void ShowResult(CopyOperation operation);
    void Error(string message);
    void Warning(string message);
    void Info(string message);
}
