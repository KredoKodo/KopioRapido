namespace KopioRapido.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
    Task<string?> SaveFileAsync(string defaultFileName, string fileExtension, string fileContent);
}
