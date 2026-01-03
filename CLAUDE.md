# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KopioRapido is a desktop file copying application built with .NET 10 and .NET MAUI, targeting **Windows and macOS only**. It provides RoboCopy-like functionality with a modern GUI, leveraging FastRsyncNet for efficient delta synchronization and an intelligent transfer engine for optimized performance.

## Key Features

- **Multiple Operation Types**: Copy, Move, Sync, Mirror, and BiDirectionalSync with intelligent file comparison
- **Intelligent Transfer Engine**: Automatically analyzes storage devices and file characteristics to select optimal copy strategies
- **Adaptive Performance**: Real-time monitoring and concurrency adjustment based on actual transfer speeds
- **Transparent Compression**: Brotli compression for network transfers (saves bandwidth, transparent to user)
- **Delta Sync**: FastRsyncNet integration for efficient resume and incremental updates
- **Smart Parallelization**: Automatic selection of sequential vs parallel mode based on storage type and file characteristics
- **Native Platform Integration**: Direct platform APIs for drag-and-drop and folder selection
- **Adaptive Window Sizing**: Golden ratio proportions (1.618:1) with intelligent screen-aware sizing and persistent window state

## Build Commands

```bash
# Build for all platforms
dotnet build

# Run on Windows
dotnet run --framework net10.0-windows10.0.19041.0

# Run on macOS
dotnet run --framework net10.0-maccatalyst

# Clean build
dotnet clean && dotnet build
```

## Architecture

### Core Components

- **Core/FileCopyEngine.cs** - Main copy engine with FastRsyncNet integration
  - `CopyAsync()` - Standard copy operation (overwrites existing files)
  - `MoveAsync()` - Copies files then deletes source files after successful transfer
  - `SyncAsync()` - One-way sync (only copies missing/newer files from source)
  - `MirrorAsync()` - Makes destination exact mirror of source (includes deletions)
  - `BiDirectionalSyncAsync()` - Two-way sync with newer-timestamp-wins conflict resolution
  - `CopyFileWithDeltaSyncAsync()` - Delta sync for files >10MB when destination exists
  - `CopyFileDirectAsync()` - Standard streaming copy with progress
  - `CopyDirectoryAsync()` - Recursive directory copying

- **Core/FileComparisonHelper.cs** - File comparison and sync planning
  - `CompareDirectoriesAsync()` - Compares source/destination by size + timestamp
  - `BuildSyncPlan()` - Determines which files to copy/delete based on operation type
  - Uses fast comparison (size + timestamp, not hash) for large directory performance

- **Core/RetryHelper.cs** - Exponential backoff retry with transient error detection (network, IO, file locks, Windows error codes like ERROR_SHARING_VIOLATION)

### Service Layer

All services registered as singletons in `MauiProgram.cs`:

| Interface | Purpose |
|-----------|---------|
| `IFileOperationService` | High-level copy/move/sync/resume API, exposes intelligence engine |
| `ILoggingService` | Operation logging to `%LocalApplicationData%/KopioRapido/Logs/` |
| `IProgressTrackerService` | Speed calculations, ETA tracking |
| `IResumeService` | State persistence to `%LocalApplicationData%/KopioRapido/Operations/` |
| `IFolderPickerService` | Platform-agnostic folder picker interface |
| `IPerformanceMonitorService` | Real-time performance monitoring and adaptive concurrency adjustment |
| `IQuickScanService` | Fast directory scanning with file categorization |

#### Intelligence Engine Services (No Interface)
- `TransferIntelligenceEngine` - Strategy selection orchestrator
- `StorageProfiler` - Storage device profiling
- `FileAnalyzer` - File collection analysis

### Platform-Specific Implementations

Located in `Platforms/{Windows|MacCatalyst}/`:
- **FolderPickerService.cs** - Native folder picker (WinUI FolderPicker / NSOpenPanel)
- **DragDropHelper.cs** - Native drag-drop bypassing broken MAUI abstraction

Platform services registered with `#if WINDOWS/#elif MACCATALYST` in `MauiProgram.cs`.

### MVVM Pattern

- **ViewModels/MainViewModel.cs** - Main window logic
- **MainPage.xaml[.cs]** - Dual-pane UI (source blue, destination green)
- All UI updates wrapped in `MainThread.BeginInvokeOnMainThread()` for thread safety

## Key Implementation Details

### Intelligence Engine Workflow

1. **User selects source and destination paths**
2. **MainViewModel.StartCopyAsync()** calls intelligence engine
3. **Parallel analysis** (2-5 seconds):
   - `StorageProfiler` benchmarks source device (10MB test)
   - `StorageProfiler` benchmarks destination device (10MB test)
   - `FileAnalyzer` categorizes files by size and type
4. **Strategy selection** based on 7 rules:
   - Network transfers → Parallel (masks latency)
   - HDD destinations → Sequential (avoid seek penalties)
   - Many small files → Parallel (reduces overhead)
   - SSD to SSD → Moderate parallel
   - Large files → Sequential (already saturate bandwidth)
   - USB 2.0 → Sequential (compatibility)
   - Default → Conservative parallel
5. **Compression decision**:
   - Enable if: network transfer + >30% compressible files + slow network
   - Estimated speedup: 1.3-10x depending on file types
6. **Copy execution** with selected strategy
7. **Adaptive monitoring**:
   - Performance sampled every 2 seconds
   - If efficiency < 70% → Reduce concurrency by 25%
   - If speed increasing → Increase concurrency by 2
8. **Rich logging** with emojis and explanations

### Transparent Compression Flow
- Activates for files >10MB when destination exists
- Reduces bandwidth significantly on network drives
- Flow: Signature generation → Delta creation → Delta application

### Native Drag-and-Drop (Critical MAUI Workaround)

MAUI's `DropGestureRecognizer` is broken - `e.Data.Properties` is empty on Windows/macOS.

**Solution**: Platform-specific `DragDropHelper.cs` bypasses MAUI:
1. Access native view via `Handler.PlatformView`
2. Attach native drag-drop events directly
3. Windows: `e.DataView.GetStorageItemsAsync()` / macOS: `UIDropInteraction`
4. Called from `MainPage.xaml.cs` `OnPageLoaded` after XAML initialization

### Resume System
- Completed files tracked with path, size, timestamp
- State persisted every 10 files as JSON
- On resume: verifies destination exists, source unchanged, then skips completed files

## Common Issues and Solutions

### Progress Bars Not Updating
ObservableCollection updates from background threads don't trigger UI updates.
```csharp
MainThread.BeginInvokeOnMainThread(() => {
    sourceFile.Progress = progress.PercentComplete;
});
```

### Start Copy Button Stays Disabled
MVVM toolkit doesn't re-evaluate CanExecute automatically.
```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(StartCopyCommand))]
private string _sourcePath = string.Empty;
```

### MAUI Drag-and-Drop Doesn't Work
Don't use `DropGestureRecognizer`. See Native Drag-and-Drop section above.

## Platform Notes

### Windows
- Requires Windows 10 SDK Build Tools (10.0.26100+)
- MVVMTK0045 warnings are safe to ignore
- Folder picker needs WinUI 3 window handle initialization

### macOS
- Requires Xcode and macOS workload
- NSOpenPanel must run on main thread
- File system permissions may require user consent

## Data Locations

- Logs: `%LocalApplicationData%/KopioRapido/Logs/{operationId}.log`
- Resume state: `%LocalApplicationData%/KopioRapido/Operations/{operationId}.json`
- Window preferences: `%LocalAppData%/KopioRapido/Preferences` (Windows), `~/Library/Preferences/com.kopiorapido.preferences.plist` (macOS)

## Recent Updates (2026-01-03)

### ✅ Intelligence Engine Fully Integrated
- **TransferIntelligenceEngine**: Analyzes storage + files, selects optimal strategy
- **StorageProfiler**: Benchmarks devices (SSD/HDD/Network/USB detection)
- **FileAnalyzer**: Categorizes files by size and compressibility
- **PerformanceMonitorService**: Real-time adaptive concurrency adjustment
- **Selective Compression**: Brotli compression for compressible files (text/code/logs) on network transfers
- **Integrated with MainViewModel**: Rich logging with emojis and explanations

### ✅ Adaptive Window Sizing with Golden Ratio
- **WindowSizer**: Calculates optimal size using golden ratio (1.618:1) and 70% of screen work area
- **WindowPreferences**: Persists window size/position across sessions using .NET MAUI Preferences API
- **Smart Validation**: Ensures saved position is on-screen, handles disconnected monitors gracefully
- **Minimum Constraints**: 750×550 minimum size, fully resizable by user
- **Screen Aware**: Adapts to 800×600 minimum screens up to 4K displays (clamped to 1600×1200 max)

See `INTELLIGENCE_ENGINE_INTEGRATION.md`, `COMPRESSION_INTEGRATION.md`, and `ADAPTIVE_WINDOW_SIZING.md` for details.

### ✅ Storage Detection (2026-01-03)
- **Windows SSD Detection**: P/Invoke DeviceIoControl queries StorageDeviceSeekPenaltyProperty to distinguish SSD from HDD
- **Windows USB Detection**: WMI queries match USB controllers (xHCI=USB3, EHCI=USB2) and device properties
- **macOS SSD Detection**: `diskutil info` parses "Solid State: Yes" with APFS filesystem fallback
- **macOS USB Detection**: `diskutil info` and `system_profiler SPUSBDataType` parse USB speed indicators
- **Network Share Detection (macOS)**: Heuristic fallback for SMB/NFS detection (paths under /Volumes/ with speed < 50 MB/s)
- **Graceful Fallbacks**: Assumes SSD/USB3 on detection failures (optimistic, allows benchmarking to compensate)
- **Dependencies**: System.Management (Windows-only) for WMI, built-in CLI tools on macOS

### ✅ Multiple Operation Types (2026-01-03)
- **Five Operation Types Implemented**:
  1. **Copy** - Standard copy (overwrites existing files, no deletions)
  2. **Move** - Copy then delete source files (frees source space)
  3. **Sync** - One-way sync (only copies missing/newer files, preserves destination-only files)
  4. **Mirror** - One-way sync with deletions (makes destination exact match of source)
  5. **BiDirectionalSync** - Two-way sync (newer timestamp wins, copies missing files both directions)

- **UI Implementation**:
  - Dropdown selector in middle pane between source/destination
  - Dynamic description panel with operation-specific explanations
  - Operation type selection required before Start button appears
  - Start button text dynamically updates based on selected operation
  - Clear buttons for source/destination panes

- **Architecture**:
  - `FileComparisonHelper` compares directories by size + timestamp (fast, no hash)
  - `IFileOperationService.StartOperationAsync()` routes to appropriate engine method
  - Each operation type has dedicated method in `FileCopyEngine`
  - Move operation only deletes source files, never destination files
  - Resume support works across all operation types
  - Strategy parameter properly propagated through `CopyFilesFromListAsync()` for compression support

### ✅ Compression & Progress Tracking (2026-01-03)
- **Selective Per-File Compression**: Only compresses text/code/logs, skips images/videos/archives
- **Real-time Compression Stats**: Live display of compression ratio, bandwidth saved, files compressed
- **Progress Bar Fix**: All operation types (Copy/Move/Sync/Mirror/BiDirectionalSync) now properly initialize progress tracker
- **Completion Summaries**: Operation-specific summaries showing files transferred, deleted, skipped, and compression statistics
- **Debug Log Export**: Export button with native save dialogs (Windows FileSavePicker, macOS UIDocumentPickerViewController)
- **Cancel Button Fix**: Responsive cancel button during operations (fire-and-forget pattern for async operations)

### ✅ Bug Fixes (2026-01-03)
1. **Compression not working in Mirror/Sync/BiDirectionalSync**: Fixed strategy parameter not being passed to `CopyFileAsync()`
2. **Progress bar stuck at 0%**: Fixed missing `SetTotalSize()` calls in Mirror/Sync/BiDirectionalSync operations
3. **Compression stats double-counting**: Fixed by tracking compression bytes only on file completion, not on every progress update
4. **Wrong compression ratio in summary**: Fixed inverted formula (now correctly shows uncompressed/compressed)
5. **Network share detection on macOS**: Added heuristic fallback for /Volumes/ paths with slow write speeds

## Current Limitations

- CLI interface: Planned but not yet implemented
- Shell integration: Context menu handlers not implemented
