# Platform-Specific Folder Picker Implementations

This document explains the native folder picker implementations for each platform supported by KopioRapido.

## Overview

The `IFolderPickerService` interface provides a unified API for selecting folders across all platforms. Each platform has its own implementation that uses native OS dialogs.

```csharp
public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
}
```

## Platform Implementations

### Windows (Platforms/Windows/FolderPickerService.cs)

Uses `Windows.Storage.Pickers.FolderPicker` from WinRT APIs.

**Key Features:**
- Native Windows folder picker dialog
- Requires window handle initialization for WinUI 3
- Starts in Documents Library by default

**Implementation Details:**
```csharp
var folderPicker = new FolderPicker();

// Get window handle for WinUI 3
var window = (Application.Current?.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window);
var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
folderPicker.FileTypeFilter.Add("*");

var folder = await folderPicker.PickSingleFolderAsync();
return folder?.Path;
```

**Requirements:**
- Windows 10 version 19041 or higher
- WinUI 3 / Windows App SDK

### macOS (Platforms/MacCatalyst/FolderPickerService.cs)

Uses `UIDocumentPickerViewController` from UIKit framework (MacCatalyst).

**Key Features:**
- Native macOS file picker (via MacCatalyst/UIKit)
- Configured for directories only
- Runs on main thread (required for UI operations)

**Implementation Details:**
```csharp
var documentPicker = new UIDocumentPickerViewController(new string[] { "public.folder" }, UIDocumentPickerMode.Open);
documentPicker.AllowsMultipleSelection = false;
documentPicker.ShouldShowFileExtensions = true;

// ... event handlers ...

var viewController = Platform.GetCurrentUIViewController();
viewController?.PresentViewController(documentPicker, true, null);
```

**Requirements:**
- macOS 11.0 or higher
- UIKit framework access (standard in MacCatalyst)

### iOS (Platforms/iOS/FolderPickerService.cs)

Uses `UIAlertController` with text input (temporary implementation).

**Current Implementation:**
- Simple text input prompt
- Validates directory existence
- Runs on main thread

**Future Enhancement:**
Should use `UIDocumentPickerViewController` with proper document types:

```csharp
var documentPicker = new UIDocumentPickerViewController(
    new[] { UTType.Folder },
    UIDocumentPickerMode.Open);

documentPicker.AllowsMultipleSelection = false;
```

**Limitations:**
- iOS apps are sandboxed, limiting file system access
- Requires proper entitlements for file access
- User must explicitly grant permission

### Android (Platforms/Android/FolderPickerService.cs)

Uses `AlertDialog` with text input (temporary implementation).

**Current Implementation:**
- Simple text input prompt via AlertDialog
- Validates directory existence
- Runs on main thread

**Future Enhancement:**
Should use Storage Access Framework (SAF) with `Intent.ActionOpenDocumentTree`:

```csharp
var intent = new Intent(Intent.ActionOpenDocumentTree);
// Start activity for result and handle callback
```

**Limitations:**
- Android 11+ has scoped storage restrictions
- Requires storage permissions in AndroidManifest.xml
- SAF provides URIs, not file paths

## Registration in Dependency Injection

Platform-specific implementations are registered in `MauiProgram.cs` using conditional compilation:

```csharp
#if WINDOWS
builder.Services.AddSingleton<IFolderPickerService, Platforms.Windows.FolderPickerService>();
#elif MACCATALYST
builder.Services.AddSingleton<IFolderPickerService, Platforms.MacCatalyst.FolderPickerService>();
#elif IOS
builder.Services.AddSingleton<IFolderPickerService, Platforms.iOS.FolderPickerService>();
#elif ANDROID
builder.Services.AddSingleton<IFolderPickerService, Platforms.Android.FolderPickerService>();
#endif
```

This ensures the correct implementation is registered for each platform at build time.

## Usage in ViewModels

Inject `IFolderPickerService` through constructor:

```csharp
public class MainViewModel : ObservableObject
{
    private readonly IFolderPickerService _folderPicker;

    public MainViewModel(IFolderPickerService folderPicker)
    {
        _folderPicker = folderPicker;
    }

    [RelayCommand]
    private async Task SelectFolderAsync()
    {
        var folder = await _folderPicker.PickFolderAsync();
        if (folder != null)
        {
            // Use the selected folder path
        }
    }
}
```

## Testing

To test the folder pickers:

1. **Windows**: Run with `dotnet run --framework net10.0-windows10.0.19041.0`
2. **macOS**: Run with `dotnet run --framework net10.0-maccatalyst`
3. **iOS**: Deploy to simulator or device
4. **Android**: Deploy to emulator or device

## Known Issues and Limitations

### Windows
- Requires window handle initialization (specific to WinUI 3)
- May require capabilities in Package.appxmanifest

### macOS
- NSOpenPanel must run on main thread
- May require entitlements for file access

### iOS
- Current implementation is placeholder
- Real implementation needs UIDocumentPickerViewController
- Sandboxing limits direct file system access

### Android
- Current implementation is placeholder
- Real implementation needs Storage Access Framework
- Returns content URIs, not file paths
- Requires runtime permissions on Android 6+

## Future Improvements

1. **iOS**: Implement `UIDocumentPickerViewController` for native folder selection
2. **Android**: Implement SAF with proper URI handling
3. **All Platforms**: Add folder validation and error handling
4. **All Platforms**: Add configuration options (start location, multi-select, etc.)
5. **Cross-Platform**: Create fallback picker for unsupported scenarios

## Performance Considerations

- **Windows**: FolderPicker is lightweight, no performance concerns
- **macOS**: NSOpenPanel may block UI thread, consider showing loading indicator
- **iOS/Android**: Text input is immediate, native pickers add minimal overhead

## Security Considerations

- Always validate returned paths
- Check directory existence before use
- Handle permission denied scenarios gracefully
- Never expose system directories without user confirmation
- Respect platform sandboxing restrictions

## References

- [Windows FolderPicker](https://learn.microsoft.com/uwp/api/windows.storage.pickers.folderpicker)
- [macOS NSOpenPanel](https://developer.apple.com/documentation/appkit/nsopenpanel)
- [iOS UIDocumentPickerViewController](https://developer.apple.com/documentation/uikit/uidocumentpickerviewcontroller)
- [Android Storage Access Framework](https://developer.android.com/guide/topics/providers/document-provider)
