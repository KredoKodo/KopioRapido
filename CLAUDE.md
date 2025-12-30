# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KopioRapido is a cross-platform, high-performance file copying application built with .NET 10 and .NET MAUI. It provides RoboCopy-like functionality with a modern GUI and CLI interface, leveraging the FastRsyncNet library for efficient delta synchronization.

**Primary Goals:**
- Provide the fastest possible file copying with reliable, resumable operations
- Support local directories, network drives, and external drives
- Offer both GUI and CLI interfaces in a single application
- Use native OS folder pickers for best user experience
- Integrate with OS shell context menus (Windows/macOS)

## Technology Stack

- **.NET 10** - Target framework
- **.NET MAUI** - Cross-platform UI framework
- **FastRsyncNet** (v2.4.4) - Delta synchronization library for efficient file transfers
- **System.CommandLine** (v2.0.1) - Command-line parsing (for future CLI implementation)
- **CommunityToolkit.Mvvm** (v8.4.0) - MVVM infrastructure

## Project Structure

```
KopioRapido/
├── Core/                      # Business logic layer
│   ├── FileCopyEngine.cs     # Main file copying engine with FastRsyncNet integration
│   └── RetryHelper.cs        # Retry logic with exponential backoff and transient error detection
├── Services/                  # Service layer
│   ├── IFileOperationService.cs / FileOperationService.cs
│   ├── ILoggingService.cs / LoggingService.cs
│   ├── IProgressTrackerService.cs / ProgressTrackerService.cs
│   ├── IResumeService.cs / ResumeService.cs
│   └── IFolderPickerService.cs  # Platform-agnostic folder picker interface
├── Models/                    # Data models
│   ├── CopyOperation.cs      # Represents a copy operation with status/progress
│   ├── FileTransferProgress.cs # Progress tracking for individual files
│   ├── FileItem.cs           # Individual file display model with progress tracking
│   ├── RetryConfiguration.cs # Retry policy configuration
│   └── OperationLog.cs       # Logging data model
├── ViewModels/               # MVVM ViewModels
│   └── MainViewModel.cs      # Main window ViewModel
├── Views/                     # MAUI pages (currently none, MainPage is in root)
├── Converters/               # XAML value converters
│   ├── BoolInverterConverter.cs
│   ├── PercentToProgressConverter.cs
│   └── IsStringNotNullOrEmptyConverter.cs
├── Platforms/                # Platform-specific implementations
│   ├── Windows/
│   │   ├── FolderPickerService.cs       # Native Windows folder picker
│   │   └── DragDropHelper.cs            # Native WinUI drag-drop handler (bypasses MAUI)
│   ├── MacCatalyst/FolderPickerService.cs   # Native macOS folder picker
│   ├── iOS/FolderPickerService.cs           # iOS folder picker (text input for now)
│   └── Android/FolderPickerService.cs       # Android folder picker (text input for now)
├── CLI/                      # Command-line interface (future implementation)
├── MainPage.xaml[.cs]        # Main dual-pane UI (in root directory)
└── MauiProgram.cs            # DI registration and app configuration
```

## Architecture

### Core Copy Engine (Core/FileCopyEngine.cs)

The `FileCopyEngine` class is the heart of KopioRapido:

- **Delta Sync Auto-detection**: Automatically uses FastRsyncNet delta sync for files >10MB when destination exists
- **Progress Tracking**: Real-time progress updates for both individual files and overall operation
- **Resume Support**: All operations save state for automatic resumption after interruption
- **Parallel Processing**: Uses async/await throughout for non-blocking operations
- **Error Handling**: Comprehensive logging and automatic retry logic with exponential backoff
- **Retry Logic**: Automatically retries transient errors (network issues, file locks, timeouts)

Key methods:
- `CopyAsync()` - Main entry point for copy operations, wraps operations in retry logic
- `CopyFileWithDeltaSyncAsync()` - Uses FastRsyncNet for efficient updates
- `CopyFileDirectAsync()` - Standard copy with streaming and progress
- `CopyDirectoryAsync()` - Recursive directory copying

### Retry System (Core/RetryHelper.cs)

The `RetryHelper` provides automatic retry with exponential backoff:

- **Transient Error Detection**: Identifies retryable errors (network, IO, locking)
- **Exponential Backoff**: Configurable delay multiplier (default 2.0x)
- **Jitter**: Adds randomness to prevent thundering herd
- **Error Classification**: Windows error codes (ERROR_SHARING_VIOLATION, ERROR_LOCK_VIOLATION, etc.)
- **Cancellation Support**: Respects cancellation tokens, doesn't retry cancelled operations

Configuration (RetryConfiguration):
- MaxRetryAttempts: Default 3
- InitialRetryDelayMs: Default 1000ms (1 second)
- MaxRetryDelayMs: Default 30000ms (30 seconds)
- BackoffMultiplier: Default 2.0
- UseJitter: Default true

### Service Layer

**IFileOperationService**: High-level API for file operations (copy, move, sync, resume)

**ILoggingService**: Detailed operation logging to both memory and disk
- Logs stored in: `%LocalApplicationData%/KopioRapido/Logs/`
- Format: `{operationId}.log`

**IProgressTrackerService**: Real-time speed calculations and progress tracking
- Calculates current speed, average speed, ETA
- Tracks both file-level and operation-level progress

**IResumeService**: Manages operation state persistence
- State files: `%LocalApplicationData%/KopioRapido/Operations/{operationId}.json`
- Automatically detects resumable operations on startup

**IFolderPickerService**: Platform-agnostic folder selection interface
- Each platform has its own implementation in `Platforms/{Platform}/FolderPickerService.cs`
- Windows: Uses `Windows.Storage.Pickers.FolderPicker`
- macOS: Uses `NSOpenPanel` from AppKit
- iOS/Android: Currently text input (upgradable to native pickers)

See [PLATFORM_FOLDER_PICKERS.md](PLATFORM_FOLDER_PICKERS.md) for implementation details.

### MAUI GUI

The GUI provides a dual-pane interface with:
- **Left Pane (Source)**: Blue-bordered pane showing files to be copied
  - Drag-and-drop support for folders (Windows native implementation)
  - Browse button with native folder picker
  - Real-time file list with individual progress bars
  - Files disappear from source as they complete copying
- **Right Pane (Destination)**: Green-bordered pane showing copied files
  - Drag-and-drop support for folders (Windows native implementation)
  - Browse button with native folder picker
  - Files appear here as they complete copying
  - Shows final file count and sizes
- **Progress Display**:
  - Individual progress bars under each file being copied
  - Overall operation progress bar
  - Real-time percentage updates
- **Real-time Stats**: Current speed, average speed, ETA
- **Activity Log**: Scrolling debug log at bottom of window
- **File Transfer Visualization**: Files move from left pane to right pane as they copy

## Build Commands

### Building the Project
```bash
dotnet build
```

### Running on Windows
```bash
dotnet build -f net10.0-windows10.0.19041.0
dotnet run --framework net10.0-windows10.0.19041.0
```

### Running on macOS
```bash
dotnet build -f net10.0-maccatalyst
dotnet run --framework net10.0-maccatalyst
```

### Restore Packages
```bash
dotnet restore
```

### Clean Build
```bash
dotnet clean
dotnet build
```

## Development Notes

### FastRsyncNet Integration

The application uses FastRsyncNet for delta synchronization:

1. **Signature Generation**: Create signature of destination file
2. **Delta Creation**: Compare source with signature, generate delta
3. **Delta Application**: Apply delta to destination

This approach significantly reduces bandwidth for large file updates on network drives.

**Threshold**: Delta sync only activates for files >10MB where destination exists.

### Native Folder Pickers

Each platform uses its native folder selection dialog for the best user experience:

- **Windows**: `Windows.Storage.Pickers.FolderPicker` with WinUI 3 window handle initialization
- **macOS**: `NSOpenPanel` from AppKit, must run on main thread
- **iOS**: Currently simple text input via `UIAlertController` (upgradable to `UIDocumentPickerViewController`)
- **Android**: Currently simple text input via `AlertDialog` (upgradable to Storage Access Framework)

**Registration**: Platform-specific implementations are registered in `MauiProgram.cs` using conditional compilation (`#if WINDOWS`, etc.)

### Native Drag-and-Drop (Windows)

**Important**: MAUI's drag-drop abstraction is incomplete on Windows. The `DataPackageView` has no properties and doesn't expose platform APIs.

**Solution**: `Platforms/Windows/DragDropHelper.cs` bypasses MAUI entirely:

1. **Direct Platform Access**: Gets the native WinUI `FrameworkElement` from the MAUI Border's handler
2. **Native Events**: Attaches Windows-specific `DragOver` and `Drop` event handlers
3. **StorageItems API**: Uses `e.DataView.GetStorageItemsAsync()` to get dropped files
4. **Integration**: Called from `MainPage.xaml.cs` `OnPageLoaded` event after XAML initialization

Key implementation details:
- MAUI's `DropGestureRecognizer` is NOT used (it doesn't work)
- Borders must have `x:Name` attributes for lookup via `FindByName()`
- Drop handler receives native Windows `DragEventArgs`, not MAUI's
- Supports both files and folders via `StorageItems[0].Path`

### Progress Tracking Implementation

Progress is reported through two mechanisms:
1. `IProgress<FileTransferProgress>` - Per-file progress updates every 100ms
2. `IProgressTrackerService` - Aggregated operation statistics

Speed calculations:
- **Current Speed**: Bytes transferred / elapsed time for current file
- **Average Speed**: Total bytes / total elapsed time for operation
- **ETA**: Remaining bytes / average speed

**UI Thread Marshalling**: All progress updates in `MainViewModel.UpdateProgress()` are wrapped in `MainThread.BeginInvokeOnMainThread()` to ensure ObservableCollection operations happen on the UI thread, enabling smooth progress bar animations and real-time file list updates.

### Resumability

Operations are resumable through:
1. Periodic state saves during copying
2. JSON persistence of `CopyOperation` objects
3. Automatic detection of incomplete operations on startup

**Note**: Current implementation restarts from beginning on resume. Future enhancement should track completed files.

### Dependency Injection

All services are registered in `MauiProgram.cs`:
- Core services: Singleton lifetime (LoggingService, ProgressTrackerService, etc.)
- Platform-specific services: Singleton lifetime (FolderPickerService)
- ViewModels: Transient lifetime
- Pages: Transient lifetime

## Completed Features (December 2025)

### ✅ Implemented

1. **Drag-and-Drop (Windows)**: ✅ COMPLETED
   - Native WinUI drag-drop implementation bypassing MAUI
   - Support for both files and folders
   - Works on both source and destination panes
   - Visual feedback during drag operations (border color changes)

2. **Retry Logic with Exponential Backoff**: ✅ COMPLETED
   - Automatic retry for transient errors (network, IO, file locks)
   - Configurable retry attempts (default: 3)
   - Exponential backoff with jitter
   - Windows error code detection (sharing violations, lock violations, etc.)
   - UI feedback showing retry attempts and status

3. **Individual File Progress Tracking**: ✅ COMPLETED
   - Real-time progress bars under each file
   - Files move from source pane to destination pane as they complete
   - Percentage display and visual indicators
   - Retry status indicators (orange color when retrying)

4. **Dual-Pane File Visualization**: ✅ COMPLETED
   - Source pane (blue) shows files to be copied with progress
   - Destination pane (green) shows completed files
   - Files automatically removed from source as they finish
   - Real-time file counts and sizes

5. **Native Folder Pickers**: ✅ COMPLETED
   - Windows: Native WinUI folder picker
   - macOS: Native NSOpenPanel
   - Browse buttons in both panes
   - Command notification for button state management

6. **UI Thread Safety**: ✅ COMPLETED
   - All ObservableCollection updates marshalled to UI thread
   - Smooth progress bar animations
   - No UI freezing during copy operations

## Known Limitations & TODOs

### Current Limitations

1. **Resume Logic**: Restarts entire operation rather than continuing from last file
2. **CLI Interface**: Structure exists but not implemented
3. **Shell Integration**: Windows/macOS context menu integration not implemented
4. **iOS/Android Drag-Drop**: Not implemented (only Windows has native drag-drop)
5. **iOS/Android Folder Pickers**: Using text input placeholders instead of native pickers
6. **Drag-Drop on macOS**: Not yet implemented (only Windows currently)

### Planned Enhancements

1. **Smart Resume**:
   - Track completed files in resume state
   - Skip already-copied files on resume
   - Verify file integrity before skipping

2. **Native Folder Pickers for Mobile**:
   - iOS: Implement `UIDocumentPickerViewController`
   - Android: Implement Storage Access Framework with `Intent.ActionOpenDocumentTree`

3. **CLI Implementation**:
   - Use `System.CommandLine` for parsing
   - Commands: `copy`, `move`, `sync`, `resume`, `list`
   - Support same arguments as RoboCopy

4. **Shell Extension**:
   - Windows: Registry-based context menu handler
   - macOS: Finder extension
   - Launch GUI with source pre-filled

5. **Drag-and-Drop for macOS**:
   - Implement native macOS drag-drop handler similar to Windows
   - Use NSPasteboard and file promises

6. **Additional Operations**:
   - Move operations (copy + delete source)
   - Sync operations (bidirectional comparison and sync)
   - Verification mode (compare without copying)

## Common Development Tasks

### Adding a New Copy Operation Type

1. Add enum value to `CopyOperationType` in `Models/CopyOperation.cs`
2. Implement logic in `Core/FileCopyEngine.cs`
3. Add service method to `Services/IFileOperationService.cs` and implementation
4. Update UI in `ViewModels/MainViewModel.cs` and `MainPage.xaml`

### Adding a New Service

1. Create interface in `Services/I{ServiceName}.cs`
2. Create implementation in `Services/{ServiceName}.cs`
3. Register in `MauiProgram.cs` dependency injection
4. Inject where needed via constructor

### Implementing a New Platform-Specific Feature

1. Create interface in `Services/I{Feature}.cs`
2. Create platform-specific implementations in `Platforms/{Platform}/{Feature}.cs`
3. Register in `MauiProgram.cs` using `#if WINDOWS/#elif MACCATALYST` etc.
4. Inject and use via interface

### Testing File Operations

Use test files in various scenarios:
- Small files (<10MB) - Direct copy
- Large files (>10MB) - Delta sync when destination exists
- Network paths - Test `\\server\share\path` format on Windows
- Special characters - Test paths with spaces, unicode

### Debugging FastRsyncNet Issues

Enable detailed logging:
```csharp
await _loggingService.LogAsync(operationId, LogLevel.Debug,
    "Delta sync operation details...");
```

Check log files in `%LocalApplicationData%/KopioRapido/Logs/`

## Windows-Specific Notes

- Ensure Windows 10 SDK Build Tools (10.0.26100+) installed for Windows builds
- WinRT scenarios may show MVVMTK0045 warnings (safe to ignore unless targeting UWP)
- Folder picker requires window handle initialization for WinUI 3
- **Drag-Drop Issue**: MAUI's `DataPackageView` has zero properties and doesn't expose platform APIs - must use native WinUI implementation

## Common Issues and Solutions

### MAUI Drag-and-Drop Doesn't Work
**Problem**: MAUI's `DropGestureRecognizer` fires events but `e.Data.Properties` is empty.

**Root Cause**: MAUI's cross-platform abstraction doesn't properly bridge to Windows `StorageItems` API.

**Solution**:
1. Remove `DropGestureRecognizer` from XAML
2. Create platform-specific handler in `Platforms/Windows/DragDropHelper.cs`
3. Access native `FrameworkElement` via `Handler.PlatformView`
4. Attach native Windows drag-drop events
5. Use `e.DataView.GetStorageItemsAsync()` from native event args

### Progress Bars Not Updating
**Problem**: Progress bars remain at 0% even though files are copying.

**Root Cause**: ObservableCollection updates from background threads don't trigger UI updates.

**Solution**: Wrap all UI updates in `MainThread.BeginInvokeOnMainThread()`:
```csharp
MainThread.BeginInvokeOnMainThread(() =>
{
    sourceFile.Progress = progress.PercentComplete;
    SourceFiles.Remove(completedFile);
    DestinationFiles.Add(newFile);
});
```

### Start Copy Button Doesn't Enable
**Problem**: Button stays disabled even after selecting source and destination.

**Root Cause**: MVVM toolkit doesn't know to re-evaluate `CanExecute` when properties change.

**Solution**: Add `[NotifyCanExecuteChangedFor(nameof(StartCopyCommand))]` attribute:
```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(StartCopyCommand))]
private string _sourcePath = string.Empty;
```

## macOS-Specific Notes

- Requires Xcode and iOS/macOS workloads installed
- File system permissions may require explicit user consent
- NSOpenPanel must run on main thread

## Contributing Guidelines

When adding features:
- Follow existing async/await patterns
- Use dependency injection for all services
- Add detailed logging for operations
- Update progress tracking where appropriate
- Maintain resumability for long-running operations
- Test on both Windows and macOS when possible
- Use platform-specific implementations for platform features
