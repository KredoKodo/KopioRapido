using KopioRapido.Services;
using Windows.Storage.Pickers;

namespace KopioRapido.Platforms.Windows;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        var folderPicker = new FolderPicker();

        // Get the current window handle
        var window = (Application.Current?.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window);
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        }

        folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        folderPicker.FileTypeFilter.Add("*");

        var folder = await folderPicker.PickSingleFolderAsync();

        return folder?.Path;
    }

    public async Task<string?> SaveFileAsync(string defaultFileName, string fileExtension, string fileContent)
    {
        var savePicker = new FileSavePicker();

        // Get the current window handle
        var window = (Application.Current?.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window);
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        }

        savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        savePicker.SuggestedFileName = defaultFileName;
        savePicker.FileTypeChoices.Add("Text File", new List<string>() { fileExtension });

        var file = await savePicker.PickSaveFileAsync();
        
        if (file != null)
        {
            await Windows.Storage.FileIO.WriteTextAsync(file, fileContent);
            return file.Path;
        }

        return null;
    }
}