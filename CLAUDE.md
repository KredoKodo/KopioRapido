# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KopioRapido is a desktop file copying application built with .NET 10 and .NET MAUI, targeting **Windows and macOS**. It provides RoboCopy-like functionality with a modern GUI and CLI interface, leveraging the FastRsyncNet library for efficient delta synchronization.

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
│   ├── VerificationResult.cs # Verification operation results with file comparisons
│   └── OperationLog.cs       # Logging data model
├── ViewModels/               # MVVM ViewModels
│   └── MainViewModel.cs      # Main window ViewModel
├── Views/                     # MAUI pages (currently none, MainPage is in root)
├── Converters/               # XAML value converters
│   ├── BoolInverterConverter.cs
│   ├── PercentToProgressConverter.cs
│   ├── IsStringNotNullOrEmptyConverter.cs
│   └── EnumToBoolConverter.cs     # Converts CopyOperationType enum to bool for RadioButton binding
├── Graphics/                 # Custom graphics and drawable components
│   └── ProgressCircleDrawable.cs  # Animated progress circle using Microsoft.Maui.Graphics
├── Platforms/                # Platform-specific implementations
│   ├── Windows/
│   │   ├── FolderPickerService.cs       # Native Windows folder picker
│   │   ├── DragDropHelper.cs            # Native WinUI drag-drop handler (bypasses MAUI)
│   │   └── WindowEffects.cs             # Mica/Acrylic backdrop effects (DISABLED)
│   └── MacCatalyst/
│       ├── FolderPickerService.cs       # Native macOS folder picker (placeholder)
│       └── WindowEffects.cs             # UIBlurEffect frosted glass (DISABLED)
├── Resources/Images/         # SVG and image assets
│   ├── app_background.svg          # Purple-cyan gradient background
│   ├── ring_glow_idle.svg          # Cyan glowing ring (idle state)
│   ├── ring_glow_active.svg        # Orange glowing ring (active state)
│   ├── folder_icon.svg             # Purple folder icon
│   └── drive_icon.svg              # Green cloud/drive icon
├── CLI/                      # Command-line interface (future implementation)
├── MainPage.xaml[.cs]        # Modern UI with glowing circles and GraphicsView (CRASHES)
└── MauiProgram.cs            # DI registration and app configuration
```

## Architecture

### Core Copy Engine (Core/FileCopyEngine.cs)

The `FileCopyEngine` class is the heart of KopioRapido:

- **Multiple Operation Types**: Copy, Move, Mirror Sync, and Verification operations
- **Delta Sync Auto-detection**: Automatically uses FastRsyncNet delta sync for files >10MB when destination exists
- **Progress Tracking**: Real-time progress updates for both individual files and overall operation
- **Resume Support**: All operations save state for automatic resumption after interruption
- **Parallel Processing**: Uses async/await throughout for non-blocking operations
- **Error Handling**: Comprehensive logging and automatic retry logic with exponential backoff
- **Retry Logic**: Automatically retries transient errors (network issues, file locks, timeouts)

Key methods:
- `CopyAsync(operationType)` - Main entry point, routes to operation-specific handlers
- `ExecuteMoveAsync()` - Copy all files, then delete source only after complete success
- `ExecuteMirrorAsync()` - One-way sync with deletion of extra destination files
- `VerifyAsync()` - Compare directories without copying, returns detailed differences
- `CopyFileWithDeltaSyncAsync()` - Uses FastRsyncNet for efficient updates
- `CopyFileDirectAsync()` - Standard copy with streaming and progress
- `CopyDirectoryAsync()` - Recursive directory copying
- `AreFilesIdentical()` - Compares files by size and timestamp
- `DeleteFileAsync()` - Delete with retry logic

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

**IFileOperationService**: High-level API for file operations
- `StartCopyAsync()` - Copy files/directories from source to destination
- `StartMoveAsync()` - Copy all files, then delete source after complete success
- `StartMirrorAsync()` - One-way sync (make destination exactly match source)
- `StartVerifyAsync()` - Compare directories and report differences
- `ResumeCopyAsync()` - Resume interrupted operations

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

See [PLATFORM_FOLDER_PICKERS.md](PLATFORM_FOLDER_PICKERS.md) for implementation details.

### MAUI GUI

The GUI provides a dual-pane interface with:
- **Operation Type Selector**: Radio buttons for Copy, Move, Mirror Sync
  - Separate "Verify Only" button for verification mode
  - Two-way binding to ViewModel using EnumToBoolConverter
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
- **Verification Results Panel**: Color-coded statistics panel (appears after verification)
  - Green: Identical files count
  - Orange: Different files count
  - Red: Missing files count (in source but not destination)
  - Purple: Extra files count (in destination but not source)
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

**Registration**: Platform-specific implementations are registered in `MauiProgram.cs` using conditional compilation (`#if WINDOWS` and `#elif MACCATALYST`)

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

Operations are resumable through smart resume functionality:

1. **Completed File Tracking**: Each successfully copied file is recorded with:
   - Relative path from source directory
   - File size for integrity verification
   - Last modified timestamp
   - Completion timestamp

2. **Automatic State Persistence**:
   - Operation state saved every 10 files
   - JSON persistence in `%LocalApplicationData%/KopioRapido/Operations/`
   - Automatic detection of incomplete operations on startup

3. **File Integrity Verification**: Before skipping a file, the system verifies:
   - Destination file still exists
   - Source file hasn't been modified (size and timestamp match)
   - Destination file size matches expected size

4. **Smart Skip Logic**:
   - Already-completed files are skipped on resume
   - Modified or missing files are automatically re-copied
   - Resume continues from exact point of interruption

5. **Resume API**:
   - `ResumeAsync(operationId)` - Resume a specific operation
   - `GetResumableOperationsAsync()` - List all resumable operations
   - `CanResumeAsync(operationId)` - Check if operation can be resumed

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

7. **Smart Resume**: ✅ COMPLETED
   - Tracks completed files with file integrity information
   - Skips already-copied files on resume
   - Verifies file integrity (size and last modified time)
   - Detects if source or destination changed since last run
   - Automatic state persistence every 10 files
   - Resume from exact point of interruption

8. **Multiple Operation Types**: ✅ COMPLETED
   - **Copy**: Standard file copying with delta sync for large files
   - **Move**: Copy all files, then delete source only after complete success
   - **Mirror Sync**: One-way sync that makes destination exactly match source (with deletions)
   - **Verification**: Compare directories without copying, report all differences
   - UI operation selector with radio buttons
   - Color-coded verification results panel

## ⚠️ Modern UI Implementation - IN PROGRESS (December 2025)

### Current Status: Application Crashes on Startup

**CRITICAL ISSUE**: The application builds successfully but crashes immediately on launch after implementing the new modern UI design based on the mockup.

**What Was Implemented** (All code complete, not yet working):

1. **SVG Visual Assets** (Resources/Images/):
   - `app_background.svg` - Purple to cyan gradient background
   - `ring_glow_idle.svg` - Cyan glowing ring for idle state
   - `ring_glow_active.svg` - Orange glowing ring for active transfers
   - `folder_icon.svg` - Purple gradient folder icon
   - `drive_icon.svg` - Green gradient cloud/drive icon

2. **Animated Progress Circle** (Graphics/ProgressCircleDrawable.cs):
   - Custom IDrawable implementation using Microsoft.Maui.Graphics
   - Dynamic progress arc animation (0-100%)
   - State-based colors: cyan (idle) → orange (active)
   - Outer glow effect during transfers
   - Real-time percentage and status text

3. **Modern Layered UI** (MainPage.xaml):
   - Gradient background overlay (30% opacity for frosted glass effect)
   - Three-column layout: Source Circle → Progress Circle → Destination Circle
   - Glowing circular drop zones with folder/cloud icons
   - Connection lines (orange) appearing during active transfers
   - Operation type selector with Copy/Move/Mirror radio buttons
   - File transfer list: queued files (left) → completed files (right)
   - Circular control buttons: Play/Pause/Stop/Verify

4. **Platform-Specific Frosted Glass Effects** (Code exists but DISABLED):
   - `Platforms/Windows/WindowEffects.cs`:
     - Mica material (Windows 11) with fallback to Acrylic (Windows 10)
     - Native `MicaController` and `DesktopAcrylicController` APIs
   - `Platforms/MacCatalyst/WindowEffects.cs`:
     - UIBlurEffect with SystemUltraThinMaterialDark style
     - Translucent window background
   - Currently commented out in `App.xaml.cs` to diagnose crash

**What's Not Working**:
- ❌ Application crashes silently on startup (no window, no error dialog)
- ❌ Crash occurs even with frosted glass effects disabled
- ❌ Process starts then immediately terminates (exit code 127)
- ❌ No .NET Runtime errors in Windows Event Log specific to KopioRapido

**Likely Causes to Investigate**:
1. **GraphicsView Initialization**: ProgressCircleDrawable may have drawing API issues
2. **XAML Binding Errors**: New components may have invalid bindings
3. **SVG Resource Loading**: SVG files may not be loading correctly
4. **Microsoft.Maui.Graphics Dependencies**: Missing or incompatible drawing APIs
5. **RadioButton/Converter Issues**: EnumToBoolConverter may have binding problems

**Debugging Steps for Next Session**:
1. Revert MainPage.xaml to previous working version to isolate crash
2. Add try-catch blocks with logging in App.xaml.cs and MainPage.xaml.cs constructors
3. Test ProgressCircleDrawable separately in minimal MAUI project
4. Remove GraphicsView from XAML and test if app launches
5. Check SVG resource registration in .csproj file
6. Add Debug.WriteLine statements throughout initialization chain
7. Run app with Visual Studio debugger attached to catch first-chance exceptions

**Critical Files**:
- `Graphics/ProgressCircleDrawable.cs` - Custom drawable (NEW)
- `MainPage.xaml` - Completely redesigned UI layout
- `MainPage.xaml.cs` - GraphicsView wiring via `SetProgressGraphicsView()`
- `ViewModels/MainViewModel.cs` - ProgressDrawable property and Invalidate() calls
- `App.xaml.cs` - Window effects code (currently commented out)
- `Resources/Images/*.svg` - All SVG assets (5 files)
- `Converters/EnumToBoolConverter.cs` - RadioButton binding (NEW)

**Recommendation**: Start next session by reverting to the last working UI commit, then re-implement the modern UI incrementally (one component at a time) to identify the exact failure point.

## Known Limitations & TODOs

### Current Limitations

1. **CLI Interface**: Structure exists but not implemented
2. **Shell Integration**: Windows/macOS context menu integration not implemented
3. **Drag-Drop on macOS**: Not yet implemented (only Windows currently)

### Planned Enhancements

1. **CLI Implementation**:
   - Use `System.CommandLine` for parsing
   - Commands: `copy`, `move`, `sync`, `resume`, `list`
   - Support same arguments as RoboCopy

2. **Shell Extension**:
   - Windows: Registry-based context menu handler
   - macOS: Finder extension
   - Launch GUI with source pre-filled

3. **Drag-and-Drop for macOS**:
   - Implement native macOS drag-drop handler similar to Windows
   - Use NSPasteboard and file promises

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

- Requires Xcode and macOS workload installed
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
