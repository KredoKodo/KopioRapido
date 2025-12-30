using Foundation;
using AppKit;
using KopioRapido.Services;

namespace KopioRapido.Platforms.MacCatalyst;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var openPanel = new NSOpenPanel
            {
                CanChooseFiles = false,
                CanChooseDirectories = true,
                AllowsMultipleSelection = false,
                Title = "Select Folder"
            };

            var result = openPanel.RunModal();

            if (result == 1) // NSModalResponse.OK
            {
                tcs.SetResult(openPanel.Url?.Path);
            }
            else
            {
                tcs.SetResult(null);
            }
        });

        return await tcs.Task;
    }
}
