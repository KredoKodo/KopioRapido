namespace KopioRapido.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
}
