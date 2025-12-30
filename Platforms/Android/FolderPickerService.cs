using Android.Content;
using AndroidX.Activity.Result;
using KopioRapido.Services;

namespace KopioRapido.Platforms.Android;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        // Android has specific storage access framework (SAF) for folder selection
        // For now, use a simple prompt. In production, you'd use Intent.ActionOpenDocumentTree
        // with proper activity result handling

        var tcs = new TaskCompletionSource<string?>();

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                tcs.SetResult(null);
                return;
            }

            // Simple text input for now
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(activity);
            builder.SetTitle("Select Folder");
            builder.SetMessage("Enter folder path:");

            var input = new Android.Widget.EditText(activity);
            builder.SetView(input);

            builder.SetPositiveButton("OK", (sender, args) =>
            {
                var text = input.Text;
                if (!string.IsNullOrWhiteSpace(text) && Directory.Exists(text))
                {
                    tcs.SetResult(text);
                }
                else
                {
                    tcs.SetResult(null);
                }
            });

            builder.SetNegativeButton("Cancel", (sender, args) =>
            {
                tcs.SetResult(null);
            });

            builder.Show();
        });

        return await tcs.Task;
    }
}
