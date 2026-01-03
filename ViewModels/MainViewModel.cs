using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KopioRapido.Core;
using KopioRapido.Models;
using KopioRapido.Services;

namespace KopioRapido.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFileOperationService _fileOperationService;
    private readonly IProgressTrackerService _progressTracker;
    private readonly IFolderPickerService _folderPicker;
    private CancellationTokenSource? _currentOperationCts;
    private string? _currentOperationId;
    private List<FileItem> _allSourceFiles = new();
    private DateTime _operationStartTime;

    // O(1) file lookup dictionary
    private Dictionary<string, FileItem> _sourceFilesByName = new();

    // Sliding window for UI collections
    private const int MAX_VISIBLE_IN_PROGRESS = 20;
    private const int MAX_VISIBLE_COMPLETED = 20;
    private HashSet<string> _inProgressFileNames = new();
    private HashSet<string> _completedFileNames = new();

    // Simple debouncing - no complex timer needed
    private DateTime _lastUiUpdate = DateTime.MinValue;
    private const int MIN_UI_UPDATE_MS = 50; // Max 20 updates/sec

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCopyCommand))]
    [NotifyPropertyChangedFor(nameof(ShowStartButton))]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCopyCommand))]
    [NotifyPropertyChangedFor(nameof(ShowStartButton))]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyButtonText))]
    [NotifyPropertyChangedFor(nameof(CopyButtonColor))]
    [NotifyCanExecuteChangedFor(nameof(StartCopyCommand))]
    private bool _isCopying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyButtonText))]
    [NotifyPropertyChangedFor(nameof(ShowStartButton))]
    private CopyOperationType _selectedOperation = CopyOperationType.Copy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStartButton))]
    private bool _hasSelectedOperation = false;

    [ObservableProperty]
    private string _operationWarningMessage = string.Empty;

    [ObservableProperty]
    private string _operationDescription = "Choose an operation type from the dropdown box above.";

    [ObservableProperty]
    private bool _showOperationWarning = false;

    [ObservableProperty]
    private string _operationInfoMessage = string.Empty;

    [ObservableProperty]
    private bool _showOperationInfo = false;

    // Dynamic button text based on operation type and state
    public string CopyButtonText
    {
        get
        {
            if (IsCopying) return "Cancel";

            return SelectedOperation switch
            {
                CopyOperationType.Copy => "Start Copy",
                CopyOperationType.Move => "Start Move",
                CopyOperationType.Sync => "Start Sync",
                CopyOperationType.Mirror => "Start Mirror",
                CopyOperationType.BiDirectionalSync => "Start Bi-Sync",
                _ => "Start"
            };
        }
    }

    // Dynamic button color based on state
    public string CopyButtonColor => IsCopying
        ? "#EF4444" // Red when copying (cancel mode)
        : "#10B981"; // Green when ready (start mode)

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _currentFileName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowIndividualFileProgress))]
    private double _currentFileProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowIndividualFileProgress))]
    private long _currentFileSize;

    // Show individual file progress for large files (over 10MB) that are in progress
    public bool ShowIndividualFileProgress => CurrentFileSize > 10 * 1024 * 1024 && CurrentFileProgress < 100;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _currentSpeed = "0 MB/s";

    [ObservableProperty]
    private string _averageSpeed = "0 MB/s";

    [ObservableProperty]
    private string _estimatedTimeRemaining = "--:--";

    [ObservableProperty]
    private ObservableCollection<string> _logMessages = new();

    [ObservableProperty]
    private ObservableCollection<FileItem> _sourceFiles = new();

    [ObservableProperty]
    private ObservableCollection<FileItem> _destinationFiles = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStartButton))]
    private bool _hasFilesToCopy;

    // Show start button only when both paths are selected AND an operation type is chosen
    public bool ShowStartButton => !string.IsNullOrWhiteSpace(SourcePath) &&
                                   !string.IsNullOrWhiteSpace(DestinationPath) &&
                                   HasFilesToCopy &&
                                   HasSelectedOperation;

    [ObservableProperty]
    private int _totalSourceFileCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourcePaneHeader))]
    private int _filesInProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourcePaneHeader))]
    private int _filesRemaining;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DestinationPaneHeader))]
    private int _filesCompleted;

    // Intelligence Engine Display Properties
    [ObservableProperty]
    private bool _showStrategyInfo;

    [ObservableProperty]
    private string _sourceStorageType = string.Empty;

    [ObservableProperty]
    private string _sourceStorageSpeed = string.Empty;

    [ObservableProperty]
    private string _destStorageType = string.Empty;

    [ObservableProperty]
    private string _destStorageSpeed = string.Empty;

    [ObservableProperty]
    private string _strategyEmoji = string.Empty;

    [ObservableProperty]
    private string _strategyName = string.Empty;

    [ObservableProperty]
    private string _strategyReasoning = string.Empty;

    [ObservableProperty]
    private bool _compressionEnabled;

    [ObservableProperty]
    private string _compressionInfo = string.Empty;

    [ObservableProperty]
    private long _totalCompressedBytes;

    [ObservableProperty]
    private long _totalUncompressedBytes;

    [ObservableProperty]
    private double _overallCompressionRatio = 1.0;

    [ObservableProperty]
    private string _bandwidthSaved = "0 B";

    // Dynamic header text for panes during copy
    public string SourcePaneHeader => IsCopying
        ? $"{FilesInProgress} copying ({FilesRemaining:N0} remaining)"
        : $"{TotalSourceFileCount} files";

    public string DestinationPaneHeader => IsCopying
        ? $"{FilesCompleted:N0} copied"
        : $"{DestinationFiles.Count} copied";

    public MainViewModel(IFileOperationService fileOperationService, IProgressTrackerService progressTracker, IFolderPickerService folderPicker)
    {
        _fileOperationService = fileOperationService;
        _progressTracker = progressTracker;
        _folderPicker = folderPicker;
    }

    [RelayCommand]
    private async Task SelectSourceAsync()
    {
        try
        {
            var result = await _folderPicker.PickFolderAsync();
            if (result != null)
            {
                SourcePath = result;
                AddLogMessage($"Source selected: {SourcePath}");
                await ScanSourceDirectoryAsync();
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error selecting source: {ex.Message}");
        }
    }

    private async Task ScanSourceDirectoryAsync()
    {
        SourceFiles.Clear();
        DestinationFiles.Clear();
        _sourceFilesByName.Clear();

        if (string.IsNullOrWhiteSpace(SourcePath) || !Directory.Exists(SourcePath))
        {
            HasFilesToCopy = false;
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                var files = Directory.GetFiles(SourcePath, "*", SearchOption.AllDirectories);
                var fileItems = new List<FileItem>();
                var filesByName = new Dictionary<string, FileItem>();

                foreach (var filePath in files)
                {
                    var fileInfo = new FileInfo(filePath);
                    var relativePath = Path.GetRelativePath(SourcePath, filePath);

                    var fileItem = new FileItem
                    {
                        FileName = relativePath,
                        FullPath = filePath,
                        SizeBytes = fileInfo.Length,
                        FormattedSize = FormatBytes(fileInfo.Length),
                        Progress = 0,
                        IsDirectory = false,
                        IsCopying = false,
                        IsCompleted = false
                    };
                    fileItems.Add(fileItem);
                    filesByName[relativePath] = fileItem;
                }

                // Store all files internally for copying
                _allSourceFiles = fileItems;
                _sourceFilesByName = filesByName;

                // Only show preview (first 20 files) in UI for fast loading
                var preview = fileItems.Take(20).ToList();
                var totalCount = fileItems.Count;
                var remainingCount = totalCount - preview.Count;

                Application.Current?.Dispatcher.Dispatch(() =>
                {
                    SourceFiles.Clear();
                    foreach (var item in preview)
                    {
                        SourceFiles.Add(item);
                    }

                    TotalSourceFileCount = totalCount;
                    FilesRemaining = totalCount;
                    HasFilesToCopy = totalCount > 0;

                    if (remainingCount > 0)
                    {
                        AddLogMessage($"Showing 20 of {totalCount} files ({remainingCount} more files ready to copy)");
                    }
                    else
                    {
                        AddLogMessage($"Found {totalCount} files to copy");
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Dispatch(() =>
                {
                    AddLogMessage($"Error scanning directory: {ex.Message}");
                    HasFilesToCopy = false;
                });
            }
        });
    }

    [RelayCommand]
    private async Task SelectDestinationAsync()
    {
        try
        {
            var result = await _folderPicker.PickFolderAsync();
            if (result != null)
            {
                DestinationPath = result;
                AddLogMessage($"Destination selected: {DestinationPath}");
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error selecting destination: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ClearSource()
    {
        SourcePath = string.Empty;
        SourceFiles.Clear();
        _allSourceFiles.Clear();
        _sourceFilesByName.Clear();
        HasFilesToCopy = false;
        TotalSourceFileCount = 0;
        AddLogMessage("Source cleared");
    }

    [RelayCommand]
    private void ClearDestination()
    {
        DestinationPath = string.Empty;
        DestinationFiles.Clear();
        AddLogMessage("Destination cleared");
    }

    [RelayCommand]
    private void SelectOperation(string operationType)
    {
        if (Enum.TryParse<CopyOperationType>(operationType, out var operation))
        {
            SelectedOperation = operation;
            HasSelectedOperation = true; // User has now explicitly selected an operation

            // Set description, warnings, and info messages based on operation type
            (OperationDescription, OperationWarningMessage, ShowOperationWarning, OperationInfoMessage, ShowOperationInfo) = operation switch
            {
                CopyOperationType.Copy => (
                    "Copy all files from the source to destination. No files will be deleted during this operation.",
                    string.Empty,
                    false,
                    string.Empty,
                    false
                ),
                CopyOperationType.Move => (
                    "Copies all files from source to destination, then deletes the source files after successful transfer. This frees up space on the source drive.",
                    "WARNING: Source files will be permanently deleted after the transfer completes. Ensure you have backups if needed.",
                    true,
                    string.Empty,
                    false
                ),
                CopyOperationType.Sync => (
                    "One-way synchronization from source to destination. Only copies files that are missing or newer in the source. Identical files are skipped. Files that only exist in the destination are preserved.",
                    string.Empty,
                    false,
                    string.Empty,
                    false
                ),
                CopyOperationType.Mirror => (
                    "Makes the destination an exact mirror of the source. Copies missing and newer files, then deletes any files in the destination that don't exist in the source.",
                    "WARNING: Files in the destination that don't exist in the source will be permanently deleted. This operation cannot be undone.",
                    true,
                    string.Empty,
                    false
                ),
                CopyOperationType.BiDirectionalSync => (
                    "Two-way synchronization between source and destination. Files are copied in both directions based on which version is newer. If a file exists in only one location, it's copied to the other. Conflicts are resolved by timestamp (newer wins).",
                    string.Empty,
                    false,
                    string.Empty,
                    false
                ),
                _ => (
                    "Choose an operation type from the dropdown box above.",
                    string.Empty,
                    false,
                    string.Empty,
                    false
                )
            };

            AddLogMessage($"Operation type changed to: {operation}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartCopy))]
    private void StartCopy()
    {
        // If already copying, this acts as cancel button - handle immediately and synchronously
        if (IsCopying)
        {
            AddLogMessage("🛑 Cancel requested - stopping operation...");
            _currentOperationCts?.Cancel();
            StatusMessage = "Cancelling...";
            return;
        }
        
        if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(DestinationPath))
        {
            StatusMessage = "Please select both source and destination";
            return;
        }

        // Start new operation as fire-and-forget (allows button to remain responsive)
        _ = StartNewOperationAsync();
    }

    private async Task StartNewOperationAsync()
    {
        IsCopying = true;
        _operationStartTime = DateTime.Now;
        _currentOperationId = null; // Reset so new operation ID is picked up from progress
        StatusMessage = $"Starting {SelectedOperation.ToString().ToLower()}...";
        _currentOperationCts = new CancellationTokenSource();
        LogMessages.Clear();

        // Clear progress tracking state and initialize counts
        _inProgressFileNames.Clear();
        _completedFileNames.Clear();
        _lastUiUpdate = DateTime.MinValue;
        FilesCompleted = 0;
        FilesInProgress = 0;
        FilesRemaining = _allSourceFiles.Count;

        // Reset compression metrics for new operation
        TotalCompressedBytes = 0;
        TotalUncompressedBytes = 0;
        OverallCompressionRatio = 1.0;
        BandwidthSaved = "0 B";

        // Clear UI collections for fresh start - will be populated as files are processed
        SourceFiles.Clear();
        DestinationFiles.Clear();

        AddLogMessage($"Starting {SelectedOperation} from {SourcePath} to {DestinationPath}");
        AddLogMessage($"Total files: {_allSourceFiles.Count}");

        var progress = new Progress<FileTransferProgress>(UpdateProgress);

        try
        {
            // Use intelligence engine to analyze and select optimal strategy
            AddLogMessage("Analyzing storage and selecting optimal transfer strategy...");
            
            var (sourceProfile, destProfile, fileProfile, strategy) = await Task.Run(async () => 
                await _fileOperationService.AnalyzeAndSelectStrategyAsync(
                    SourcePath, 
                    DestinationPath, 
                    _currentOperationCts.Token).ConfigureAwait(false)).ConfigureAwait(false);
            
            // Generate user-friendly message
            var strategyMessage = _fileOperationService.GenerateStrategyMessage(sourceProfile, destProfile, fileProfile, strategy);
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Update strategy info for UI
                ShowStrategyInfo = true;
                SourceStorageType = sourceProfile.FriendlyName;
                SourceStorageSpeed = $"{sourceProfile.SequentialWriteMBps:F0} MB/s";
                DestStorageType = destProfile.FriendlyName;
                DestStorageSpeed = $"{destProfile.SequentialWriteMBps:F0} MB/s";
                
                // Extract emoji from strategy message
                StrategyEmoji = GetStrategyEmoji(strategy);
                StrategyName = strategy.UserFriendlyDescription;
                StrategyReasoning = strategy.Reasoning;
                
                CompressionEnabled = strategy.UseCompression;
                if (strategy.UseCompression)
                {
                    CompressionInfo = "Enabled for compressible files";
                }
                else
                {
                    CompressionInfo = string.Empty;
                }
                
                AddLogMessage($"📊 Storage Analysis:");
                AddLogMessage($"  • Source: {sourceProfile.FriendlyName} ({sourceProfile.SequentialWriteMBps:F0} MB/s)");
                AddLogMessage($"  • Destination: {destProfile.FriendlyName} ({destProfile.SequentialWriteMBps:F0} MB/s)");
                AddLogMessage($"  • Files: {fileProfile.Summary}");
                AddLogMessage($"");
                AddLogMessage($"🎯 Selected Strategy: {strategyMessage}");
                AddLogMessage($"  • Reasoning: {strategy.Reasoning}");
                if (strategy.UseCompression)
                {
                    AddLogMessage($"  • Compression: Enabled for compressible files");
                }
                AddLogMessage($"");
            });
            
            // Calculate total size from already-scanned files to avoid rescanning
            long totalBytes = _allSourceFiles.Sum(f => f.SizeBytes);
            
            StatusMessage = $"{SelectedOperation}...";

            // Store totals in strategy so FileCopyEngine can skip analysis
            strategy.PreCalculatedTotalFiles = _allSourceFiles.Count;
            strategy.PreCalculatedTotalBytes = totalBytes;

            // Yield to allow UI to update before starting heavy work
            await Task.Yield();

            // Run operation on background thread to keep UI responsive
            var operation = await Task.Run(async () =>
                await _fileOperationService.StartOperationAsync(
                    SourcePath,
                    DestinationPath,
                    SelectedOperation,
                    progress,
                    _currentOperationCts.Token,
                    strategy).ConfigureAwait(false)).ConfigureAwait(false);

            _currentOperationId = operation.Id;

            var endTime = DateTime.Now;
            var duration = endTime - _operationStartTime;

            // Ensure completion handling is on UI thread (after ConfigureAwait(false))
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (operation.Status == CopyStatus.Completed)
                {
                    StatusMessage = $"{SelectedOperation} completed successfully";
                    AddLogMessage($"{SelectedOperation} completed: {operation.FilesTransferred} files, {FormatBytes(operation.BytesTransferred)} transferred");
                    OverallProgress = 100;

                    // Show completion summary
                    await ShowCompletionSummaryAsync(operation, _operationStartTime, endTime, duration);
                }
                else if (operation.Status == CopyStatus.Failed)
                {
                    StatusMessage = $"Copy failed: {operation.ErrorMessage}";
                    AddLogMessage($"Copy failed: {operation.ErrorMessage}");
                }
                else if (operation.Status == CopyStatus.Cancelled)
                {
                    StatusMessage = "Copy cancelled";
                    AddLogMessage("Copy cancelled by user");
                }
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusMessage = $"Error: {ex.Message}";
                AddLogMessage($"Error: {ex.Message}");
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsCopying = false;
            });
            _currentOperationCts?.Dispose();
            _currentOperationCts = null;
        }
    }

    private bool CanStartCopy() => (!IsCopying && !string.IsNullOrWhiteSpace(SourcePath) && !string.IsNullOrWhiteSpace(DestinationPath)) || IsCopying;

    private void ResetAfterCompletion()
    {
        // Clear all collections and tracking
        SourceFiles.Clear();
        DestinationFiles.Clear();
        _sourceFilesByName.Clear();
        _allSourceFiles.Clear();
        _inProgressFileNames.Clear();
        _completedFileNames.Clear();

        // Reset counts
        FilesCompleted = 0;
        FilesInProgress = 0;
        FilesRemaining = 0;
        TotalSourceFileCount = 0;

        // Reset progress
        OverallProgress = 0;
        CurrentFileName = string.Empty;
        CurrentFileProgress = 0;
        CurrentFileSize = 0;
        CurrentSpeed = "0 MB/s";
        AverageSpeed = "0 MB/s";
        EstimatedTimeRemaining = "--:--";

        // Reset strategy info
        ShowStrategyInfo = false;
        SourceStorageType = string.Empty;
        SourceStorageSpeed = string.Empty;
        DestStorageType = string.Empty;
        DestStorageSpeed = string.Empty;
        StrategyEmoji = string.Empty;
        StrategyName = string.Empty;
        StrategyReasoning = string.Empty;
        CompressionEnabled = false;
        CompressionInfo = string.Empty;

        // Reset compression metrics
        TotalCompressedBytes = 0;
        TotalUncompressedBytes = 0;
        OverallCompressionRatio = 1.0;
        BandwidthSaved = "0 B";

        // Clear paths to force fresh selection
        SourcePath = string.Empty;
        DestinationPath = string.Empty;
        HasFilesToCopy = false;

        StatusMessage = "Ready";
    }

    private async Task ShowCompletionSummaryAsync(CopyOperation operation, DateTime startTime, DateTime endTime, TimeSpan duration)
    {
        // Count unique directories
        var directories = new HashSet<string>();
        foreach (var file in operation.CompletedFiles)
        {
            var dir = Path.GetDirectoryName(file.RelativePath);
            if (!string.IsNullOrEmpty(dir))
            {
                directories.Add(dir);
            }
        }

        // Build operation-specific summary
        string title = operation.OperationType switch
        {
            CopyOperationType.Copy => "Copy Complete",
            CopyOperationType.Move => "Move Complete",
            CopyOperationType.Sync => "Sync Complete",
            CopyOperationType.Mirror => "Mirror Complete",
            CopyOperationType.BiDirectionalSync => "Bi-Directional Sync Complete",
            _ => "Operation Complete"
        };

        var summary = $"{operation.OperationType} Operation Completed\n\n";
        
        // Core stats (all operations)
        summary += $"Files Transferred: {operation.FilesTransferred:N0}\n";
        
        // Operation-specific stats
        if (operation.OperationType == CopyOperationType.Move)
        {
            summary += $"Files Deleted from Source: {operation.FilesDeleted:N0}\n";
        }
        else if (operation.OperationType == CopyOperationType.Mirror)
        {
            summary += $"Files Deleted from Destination: {operation.FilesDeleted:N0}\n";
        }
        else if (operation.OperationType == CopyOperationType.Sync || 
                 operation.OperationType == CopyOperationType.BiDirectionalSync)
        {
            summary += $"Files Skipped (already in sync): {operation.FilesSkipped:N0}\n";
        }
        
        summary += $"Folders: {directories.Count:N0}\n";
        summary += $"Total Size: {FormatBytes(operation.BytesTransferred)}\n";
        
        // Compression stats (if compression was used)
        if (operation.FilesCompressed > 0)
        {
            var compressionRatio = operation.TotalCompressedBytes > 0 
                ? (double)operation.TotalUncompressedBytes / operation.TotalCompressedBytes 
                : 1.0;
            var bandwidthSaved = operation.TotalUncompressedBytes - operation.TotalCompressedBytes;
            
            summary += $"\n🗜️ Compression:\n";
            summary += $"Files Compressed: {operation.FilesCompressed:N0}\n";
            summary += $"Bandwidth Saved: {FormatBytes(bandwidthSaved)}\n";
            summary += $"Compression Ratio: {compressionRatio:F2}x\n";
        }
        
        summary += $"\nStarted: {startTime:g}\n";
        summary += $"Finished: {endTime:g}\n";
        summary += $"Duration: {FormatDuration(duration)}\n\n";
        summary += $"Average Speed: {FormatBytes((long)(operation.BytesTransferred / duration.TotalSeconds))}/s";

        // Reset everything BEFORE showing the alert
        ResetAfterCompletion();

        // Use modern window-based approach
        var window = Application.Current?.Windows?.FirstOrDefault();
        if (window?.Page != null)
        {
            await window.Page.DisplayAlertAsync(title, summary, "OK");
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
        }
        else if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }
        else
        {
            return $"{duration.Seconds}s";
        }
    }

    public async Task SetSourcePathAsync(string path)
    {
        SourcePath = path;
        AddLogMessage($"Source path set: {path}");
        await ScanSourceDirectoryAsync();
    }

    private void UpdateProgress(FileTransferProgress progress)
    {
        // Capture operation ID immediately
        if (_currentOperationId == null && !string.IsNullOrEmpty(progress.OperationId))
        {
            _currentOperationId = progress.OperationId;
        }

        // For large files (>10MB), bypass debouncing completely to ensure smooth progress
        var isLargeFile = progress.FileSize > 10 * 1024 * 1024;

        if (isLargeFile)
        {
            // No debouncing for large files - process every update immediately
            MainThread.BeginInvokeOnMainThread(() => ApplyProgressUpdate(progress));
            return;
        }

        // For small files, use less aggressive debouncing to show in-progress count
        var now = DateTime.UtcNow;
        var isCompletion = progress.PercentComplete >= 100;
        var isNewFile = progress.FileName != CurrentFileName;
        var shouldUpdateUi = isCompletion || isNewFile ||
                           (now - _lastUiUpdate).TotalMilliseconds >= MIN_UI_UPDATE_MS;

        if (!shouldUpdateUi) return;
        _lastUiUpdate = now;

        // Dispatch to UI thread
        MainThread.BeginInvokeOnMainThread(() => ApplyProgressUpdate(progress));
    }

    private void ApplyProgressUpdate(FileTransferProgress progress)
    {
        try
        {
            var fileName = progress.FileName;
            var isCompletion = progress.PercentComplete >= 100;

            // Update current file info - just simple properties, NO collections
            CurrentFileName = fileName;
            CurrentFileSize = progress.FileSize;
            CurrentFileProgress = progress.PercentComplete;

            // Track compression metrics ONLY on file completion to avoid double-counting
            if (isCompletion && progress.IsCompressed && !_completedFileNames.Contains(fileName))
            {
                TotalUncompressedBytes += progress.BytesTransferred;
                TotalCompressedBytes += progress.CompressedBytesTransferred;
                
                if (TotalCompressedBytes > 0)
                {
                    OverallCompressionRatio = (double)TotalUncompressedBytes / TotalCompressedBytes;
                    var saved = TotalUncompressedBytes - TotalCompressedBytes;
                    BandwidthSaved = FormatBytes(saved);
                }
            }

            if (_currentOperationId != null)
            {
                var currentSpeed = _progressTracker.GetCurrentSpeed(_currentOperationId);
                var avgSpeed = _progressTracker.GetAverageSpeed(_currentOperationId);

                CurrentSpeed = $"{FormatBytes((long)currentSpeed)}/s";
                AverageSpeed = $"{FormatBytes((long)avgSpeed)}/s";
                OverallProgress = _progressTracker.GetOverallProgress(_currentOperationId);

                var eta = _progressTracker.GetEstimatedTimeRemaining(_currentOperationId);
                if (eta.HasValue)
                {
                    EstimatedTimeRemaining = FormatTimeSpan(eta.Value);
                }
            }

            // Track counts for summary view
            if (isCompletion && !_completedFileNames.Contains(fileName))
            {
                _completedFileNames.Add(fileName);
                FilesCompleted = _completedFileNames.Count;
                FilesRemaining = _allSourceFiles.Count - FilesCompleted;

                // Remove from in-progress tracking
                _inProgressFileNames.Remove(fileName);
                FilesInProgress = _inProgressFileNames.Count;

                // Log every 50 files
                if (FilesCompleted % 50 == 0 || FilesCompleted == _allSourceFiles.Count)
                {
                    AddLogMessage($"Progress: {FilesCompleted:N0} / {_allSourceFiles.Count:N0} files copied");
                }
            }
            else if (!isCompletion && !_completedFileNames.Contains(fileName))
            {
                // Add to in-progress if we see any activity on this file
                if (!_inProgressFileNames.Contains(fileName))
                {
                    _inProgressFileNames.Add(fileName);
                    FilesInProgress = _inProgressFileNames.Count;
                }
            }
        }
        catch
        {
            // Ignore UI update errors during shutdown
        }
    }

    public void AddLogMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogMessages.Insert(0, $"[{timestamp}] {message}");

        while (LogMessages.Count > 100)
        {
            LogMessages.RemoveAt(LogMessages.Count - 1);
        }
    }

    [RelayCommand]
    private async Task ExportLogAsync()
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var defaultFileName = $"KopioRapido_Log_{timestamp}.txt";

            // Reverse the log messages so oldest is first
            var logContent = string.Join(Environment.NewLine, LogMessages.Reverse());

            var filePath = await _folderPicker.SaveFileAsync(defaultFileName, ".txt", logContent);
            
            if (filePath != null)
            {
                AddLogMessage($"✅ Log exported to: {filePath}");
            }
            else
            {
                AddLogMessage("Export cancelled");
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"❌ Failed to export log: {ex.Message}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F2} {suffixes[suffixIndex]}";
    }

    private static string GetStrategyEmoji(KopioRapido.Models.TransferStrategy strategy)
    {
        // Add compression emoji if enabled
        if (strategy.UseCompression)
        {
            return strategy.Mode switch
            {
                KopioRapido.Models.TransferMode.ParallelAggressive => "🚀🗜️",
                KopioRapido.Models.TransferMode.ParallelModerate => "⚡🗜️",
                KopioRapido.Models.TransferMode.ParallelConservative => "💨🗜️",
                _ => "📁🗜️"
            };
        }

        return strategy.Mode switch
        {
            KopioRapido.Models.TransferMode.ParallelAggressive => "🚀",
            KopioRapido.Models.TransferMode.ParallelModerate => "⚡",
            KopioRapido.Models.TransferMode.ParallelConservative => "💨",
            _ => "📁"
        };
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
        else
        {
            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
    }
}
