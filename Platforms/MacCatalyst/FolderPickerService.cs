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
                // Use modern API - create array of UTType for folders
                var folderType = UniformTypeIdentifiers.UTTypes.Folder;
                var documentPicker = new UIDocumentPickerViewController(new[] { folderType });
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
                    // Fallback - get root view controller from window scene
                    var window = GetKeyWindow();
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

    public async Task<string?> SaveFileAsync(string defaultFileName, string fileExtension, string fileContent)
    {
        var tcs = new TaskCompletionSource<string?>();

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                // Create a temporary file with the content
                var tempPath = Path.Combine(Path.GetTempPath(), defaultFileName);
                await File.WriteAllTextAsync(tempPath, fileContent);

                var fileUrl = NSUrl.FromFilename(tempPath);

                // Suppress CA1422 - UIDocumentPickerMode is deprecated but still functional for export scenarios
                // The modern alternative doesn't provide equivalent functionality for file export
#pragma warning disable CA1422
                var documentPicker = new UIDocumentPickerViewController(new[] { fileUrl }, UIDocumentPickerMode.ExportToService);
#pragma warning restore CA1422

                documentPicker.DidPickDocumentAtUrls += (sender, e) =>
                {
                    var url = e.Urls.FirstOrDefault();
                    // Clean up temp file
                    try { File.Delete(tempPath); } catch { }
                    tcs.TrySetResult(url?.Path);
                };

                documentPicker.WasCancelled += (sender, e) =>
                {
                    // Clean up temp file
                    try { File.Delete(tempPath); } catch { }
                    tcs.TrySetResult(null);
                };

                var viewController = Platform.GetCurrentUIViewController();
                
                if (viewController != null)
                {
                    viewController.PresentViewController(documentPicker, true, null);
                }
                else
                {
                    tcs.TrySetException(new Exception("Could not find current view controller"));
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return await tcs.Task;
    }

    private static UIWindow? GetKeyWindow()
    {
        // Modern way to get key window without deprecated KeyWindow property
        var scenes = UIApplication.SharedApplication.ConnectedScenes;
        foreach (var scene in scenes)
        {
            if (scene is UIWindowScene windowScene && windowScene.ActivationState == UISceneActivationState.ForegroundActive)
            {
                foreach (var window in windowScene.Windows)
                {
                    if (window.IsKeyWindow)
                    {
                        return window;
                    }
                }
            }
        }
        return null;
    }
}
