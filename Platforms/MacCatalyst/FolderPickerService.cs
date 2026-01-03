using Foundation;
using KopioRapido.Services;

namespace KopioRapido.Platforms.MacCatalyst;

public class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        // Placeholder implementation for macOS
        // TODO: Implement native macOS folder picker using AppKit.NSOpenPanel
        // For now, return null to allow compilation
        await Task.CompletedTask;
        return null;
    }
}
