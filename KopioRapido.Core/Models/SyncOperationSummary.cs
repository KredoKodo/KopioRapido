namespace KopioRapido.Models;

public class SyncOperationSummary
{
    public int FilesToCopy { get; set; }
    public int FilesToDelete { get; set; }
    public int IdenticalFiles { get; set; }
    public long TotalBytesToCopy { get; set; }
    public long TotalBytesToDelete { get; set; }
    public CopyOperationType OperationType { get; set; }
}
