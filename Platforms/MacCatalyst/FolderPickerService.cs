using Foundation;
using UIKit;
using KopioRapido.Services;
using Microsoft.Maui.ApplicationModel;

namespace KopioRapido.Platforms.MacCatalyst;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                var documentPicker = new UIDocumentPickerViewController(new string[] { "public.folder" }, UIDocumentPickerMode.Open);
                documentPicker.AllowsMultipleSelection = false;
                documentPicker.ShouldShowFileExtensions = true;

                documentPicker.DidPickDocumentAtUrls += (sender, e) =>
                {
                    var url = e.Urls.FirstOrDefault();
                    tcs.TrySetResult(url?.Path);
                };

                documentPicker.WasCancelled += (sender, e) =>
                {
                    tcs.TrySetResult(null);
                };

                var viewController = Platform.GetCurrentUIViewController();
                
                if (viewController != null)
                {
                    viewController.PresentViewController(documentPicker, true, null);
                }
                else
                {
                    // Fallback if Platform.GetCurrentUIViewController() returns null
                    var window = UIApplication.SharedApplication.KeyWindow;
                    var vc = window?.RootViewController;
                    while (vc?.PresentedViewController != null)
                    {
                        vc = vc.PresentedViewController;
                    }
                    
                    if (vc != null)
                    {
                        vc.PresentViewController(documentPicker, true, null);
                    }
                    else
                    {
                        tcs.TrySetException(new Exception("Could not find current view controller"));
                    }
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return await tcs.Task;
    }
}
