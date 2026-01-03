# KopioRapido

A cross-platform, high-performance file copying application built with .NET 10 and .NET MAUI. KopioRapido provides RoboCopy-like functionality with both a modern graphical interface and a powerful command-line interface, leveraging delta synchronization and intelligent transfer optimization for efficient file operations.

## Features

### Core Capabilities
- **⚡ High-Speed Copying** - Intelligent transfer engine with automatic strategy selection
- **🗜️ Smart Compression** - Transparent Brotli compression for network transfers
- **🔄 Delta Sync** - FastRsyncNet integration for efficient file updates
- **📊 Real-Time Monitoring** - Adaptive concurrency with performance tracking
- **🔄 Resumable Operations** - Automatically resume interrupted transfers
- **📝 Comprehensive Logging** - Detailed emoji-based logs for troubleshooting

### Operation Types
- **Copy** - Standard copy (overwrites existing files)
- **Move** - Copy then delete source files
- **Sync** - One-way sync (missing/newer files only)
- **Mirror** - One-way sync with deletions
- **BiDirectional Sync** - Two-way sync (newer timestamp wins)

### Dual Interface
- **🖥️ GUI Application** - Dual-pane MAUI interface with drag-and-drop support
- **⌨️ CLI Tool** - Full-featured command-line interface with JSON output
- **🎯 Native Integration** - Platform-specific folder pickers and drag-drop

## Technology

- **.NET 10** - Latest .NET framework
- **.NET MAUI** - Modern cross-platform UI framework (GUI)
- **System.CommandLine 2.0.1** - Modern CLI framework
- **Spectre.Console** - Rich terminal output with TTY detection
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
- Supported platforms:
  - **GUI**: Windows 10+, macOS 11+
  - **CLI**: Windows, macOS, Linux (cross-platform)

## Getting Started

### Build the Project

```bash
# Build all projects
dotnet build

# Build GUI only
dotnet build KopioRapido.csproj

# Build CLI only
dotnet build KopioRapido.CLI/KopioRapido.CLI.csproj

# Build for specific platform (GUI)
dotnet build -f net10.0-windows10.0.19041.0
dotnet build -f net10.0-maccatalyst
```

### Run the Application

#### GUI Application

**Windows**:
```bash
dotnet run --framework net10.0-windows10.0.19041.0
```

**macOS**:
```bash
dotnet run --framework net10.0-maccatalyst
```

#### CLI Tool

```bash
# Run directly
dotnet run --project KopioRapido.CLI -- <command> [options]

# Or build and run executable
dotnet build KopioRapido.CLI
./KopioRapido.CLI/bin/Debug/net10.0/kopiorapido <command> [options]
```

## Usage

### Command-Line Interface

#### Basic Commands

```bash
# Copy files
kopiorapido copy /source /destination

# Move files (copy then delete source)
kopiorapido move /source /destination

# One-way sync (missing/newer files only)
kopiorapido sync /source /destination

# Mirror (one-way sync with deletions)
kopiorapido mirror /source /destination

# Two-way sync (newer timestamp wins)
kopiorapido bidirectional-sync /path1 /path2

# List resumable operations
kopiorapido list

# Resume interrupted operation
kopiorapido resume <operation-id>

# Show version
kopiorapido --version

# Show help
kopiorapido --help
kopiorapido copy --help
```

#### Command Options

Each operation command supports:
- Analysis (dry-run) mode
- Custom transfer strategies
- Compression control
- Delta sync control
- Concurrency settings

*Note: Advanced options will be documented in CLI_USAGE.md*

### Graphical Interface

1. **Select Source**: Click "Select Source Folder" or drag-and-drop
2. **Select Destination**: Click "Select Destination Folder" or drag-and-drop
3. **Choose Operation**: Select Copy, Move, Sync, Mirror, or BiDirectional Sync
4. **Start Operation**: Click the operation button
5. **Monitor Progress**: View real-time progress, speed, and compression stats
6. **Cancel if Needed**: Click "Cancel" to stop (operation remains resumable)

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
