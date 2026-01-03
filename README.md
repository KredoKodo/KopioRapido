# KopioRapido

A cross-platform, high-performance file copying application built with .NET 10 and .NET MAUI. KopioRapido provides RoboCopy-like functionality with a modern graphical interface and command-line options, leveraging delta synchronization for efficient file transfers.

## Features

- **⚡ High-Speed Copying** - Optimized file transfer with smart delta synchronization
- **🔄 Resumable Operations** - Automatically resume interrupted transfers
- **📊 Real-Time Progress** - Dual progress bars showing overall and per-file progress
- **📈 Speed Monitoring** - Current speed, average speed, and ETA calculations
- **📝 Detailed Logging** - Complete operation logs for troubleshooting
- **🖥️ Cross-Platform** - Runs on Windows, macOS, iOS, and Android
- **🎯 Native Folder Pickers** - Platform-specific folder selection dialogs
- **🎯 Simple Interface** - Dual-pane design for easy source/destination selection

## Technology

- **.NET 10** - Latest .NET framework
- **.NET MAUI** - Modern cross-platform UI framework
- **FastRsyncNet** - Delta synchronization for efficient updates
- **MVVM Pattern** - Clean architecture with dependency injection

## Requirements

### For Development
- **Visual Studio 2026** or later (recommended)
- **.NET 10 SDK** or later
- **Windows**: Windows 10 version 19041 or higher
- **macOS**: macOS 11.0 or higher with Xcode installed

### For Running
- **.NET 10 Runtime**
- Supported platforms: Windows 10+, macOS 11+, iOS 14+, Android 5.0+

## Getting Started

### Build the Project

```bash
# Restore NuGet packages
dotnet restore

# Build for all platforms
dotnet build

# Build for specific platform
dotnet build -f net10.0-windows10.0.19041.0
dotnet build -f net10.0-maccatalyst
```

### Run the Application

#### Windows
```bash
dotnet run --framework net10.0-windows10.0.19041.0
```

#### macOS
```bash
dotnet run --framework net10.0-maccatalyst
```

## Usage

### Graphical Interface

1. **Select Source**: Click "Select Source Folder" - native OS dialog appears
2. **Select Destination**: Click "Select Destination Folder" - native OS dialog appears
3. **Start Copy**: Click the "Start Copy" button
4. **Monitor Progress**: View real-time progress, speed, and activity logs
5. **Cancel if Needed**: Click "Cancel" to stop the operation

## How It Works

### Smart Delta Synchronization

KopioRapido automatically detects when to use delta synchronization:

- **Small files (<10 MB)**: Direct copy for maximum speed
- **Large files (≥10 MB)**: Delta sync when destination exists
- **New files**: Always use direct copy
- **Network transfers**: Reduces bandwidth usage significantly

### Native Folder Pickers

Each platform uses its native folder selection dialog:
- **Windows**: `Windows.Storage.Pickers.FolderPicker`
- **macOS**: `NSOpenPanel` from AppKit
- **iOS/Android**: Currently text input, upgradable to native pickers

See [PLATFORM_FOLDER_PICKERS.md](PLATFORM_FOLDER_PICKERS.md) for details.

### Resumable Operations

All operations are automatically saved to disk:
- **State files**: Stored in `%LocalApplicationData%/KopioRapido/Operations/`
- **Auto-detection**: Incomplete operations detected on startup
- **Manual resume**: Resume any saved operation

## Project Structure

```
KopioRapido/
├── Core/                      # Business logic and file copy engine
├── Services/                  # Service layer (operations, logging, progress, folder picker)
├── Models/                    # Data models
├── ViewModels/                # MVVM ViewModels
├── Views/                     # MAUI pages (currently MainPage in root)
├── Converters/                # XAML value converters
├── Platforms/                 # Platform-specific implementations
│   ├── Windows/              # Windows folder picker
│   ├── MacCatalyst/          # macOS folder picker
│   ├── iOS/                  # iOS folder picker
│   └── Android/              # Android folder picker
└── CLI/                      # Command-line interface (future)
```

## Roadmap

### Planned Features

- [ ] **Command-Line Interface** - Full CLI with RoboCopy-compatible commands
- [ ] **Shell Integration** - Right-click context menu ("Copy with KopioRapido")

### Completed Features

- [x] **Drag-and-Drop Support** - Drag folders from Finder/Explorer into source/destination panes
- [x] **Multiple Operation Types** - Copy, Move, Sync, Mirror, BiDirectionalSync
- [x] **Intelligence Engine** - Automatic storage profiling and strategy selection
- [x] **Adaptive Performance** - Real-time concurrency adjustment
- [x] **Selective Compression** - Brotli compression for compressible files on network transfers
- [x] **Core file copying engine** - Optimized with FastRsyncNet delta synchronization
- [x] **Progress tracking** - Real-time speed, ETA, compression statistics
- [x] **Resumable operations** - Automatic state persistence and recovery
- [x] **Detailed logging** - Per-operation logs with rich emoji indicators
- [x] **Dual-pane GUI** - Modern interface with golden ratio sizing
- [x] **Cross-platform support** - Windows and macOS/MacCatalyst
- [x] **Native folder pickers** - Platform-specific folder selection dialogs
- [x] **Exponential backoff retry** - Automatic retry with transient error detection

## Documentation

- [CLAUDE.md](CLAUDE.md) - Detailed development documentation for Claude Code
- [PLATFORM_FOLDER_PICKERS.md](PLATFORM_FOLDER_PICKERS.md) - Native folder picker implementation details

## License

This project is licensed under the MIT License.

## Acknowledgments

- **FastRsyncNet** - Efficient delta synchronization library
- **.NET MAUI Team** - Cross-platform framework
- **RoboCopy** - Inspiration for robust file copying

---

**Note**: This project is under active development. Some features may be incomplete or subject to change.
