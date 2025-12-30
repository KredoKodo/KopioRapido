using KopioRapido.Services;
using UIKit;

namespace KopioRapido.Platforms.iOS;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        // iOS has limited folder access due to sandboxing
        // For now, use a simple prompt. In production, you'd use UIDocumentPickerViewController
        // with specific document types or directories

        var tcs = new TaskCompletionSource<string?>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var alert = UIAlertController.Create(
                "Select Folder",
                "Enter the folder path:",
                UIAlertControllerStyle.Alert);

            alert.AddTextField(textField =>
            {
                textField.Placeholder = "Folder path";
            });

            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, action =>
            {
                var text = alert.TextFields[0].Text;
                if (!string.IsNullOrWhiteSpace(text) && Directory.Exists(text))
                {
                    tcs.SetResult(text);
                }
                else
                {
                    tcs.SetResult(null);
                }
            }));

            alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, action =>
            {
                tcs.SetResult(null);
            }));

            var window = UIApplication.SharedApplication.KeyWindow;
            var viewController = window?.RootViewController;
            viewController?.PresentViewController(alert, true, null);
        });

        return await tcs.Task;
    }
}
