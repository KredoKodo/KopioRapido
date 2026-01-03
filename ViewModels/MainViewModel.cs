using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KopioRapido.Models;
using KopioRapido.Services;
using KopioRapido.Graphics;

namespace KopioRapido.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFileOperationService _fileOperationService;
    private readonly IProgressTrackerService _progressTracker;
    private readonly IFolderPickerService _folderPicker;
    private CancellationTokenSource? _currentOperationCts;
    private string? _currentOperationId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartOperationCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartOperationCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartOperationCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCopyCommand))]
    private bool _isCopying;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _currentFileName = string.Empty;

    [ObservableProperty]
    private double _currentFileProgress;

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
    private bool _hasFilesToCopy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartOperationCommand))]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    private CopyOperationType _selectedOperationType = CopyOperationType.Copy;

    [ObservableProperty]
    private VerificationResult? _verificationResult;

    [ObservableProperty]
    private bool _showVerificationResults;

    public ProgressCircleDrawable ProgressDrawable { get; } = new ProgressCircleDrawable();
    public GradientBackgroundDrawable BackgroundDrawable { get; } = new GradientBackgroundDrawable();
    public GlowingRingDrawable SourceRingDrawable { get; } = new GlowingRingDrawable { IconType = "folder" };
    public GlowingRingDrawable DestinationRingDrawable { get; } = new GlowingRingDrawable { IconType = "cloud" };

    private GraphicsView? _progressGraphicsView;
    private GraphicsView? _sourceRingGraphicsView;
    private GraphicsView? _destinationRingGraphicsView;

    public MainViewModel(IFileOperationService fileOperationService, IProgressTrackerService progressTracker, IFolderPickerService folderPicker)
    {
        DiagnosticLogger.Log("=== MainViewModel constructor START ===");
        try
        {
            DiagnosticLogger.Log("Injecting services...");
            _fileOperationService = fileOperationService;
            _progressTracker = progressTracker;
            _folderPicker = folderPicker;
            DiagnosticLogger.Log("=== MainViewModel constructor SUCCESS ===");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.LogException("MainViewModel constructor", ex);
            throw;
        }
    }

    public void SetProgressGraphicsView(GraphicsView graphicsView)
    {
        DiagnosticLogger.Log("=== SetProgressGraphicsView START ===");
        try
        {
            _progressGraphicsView = graphicsView;
            DiagnosticLogger.Log($"GraphicsView set: {graphicsView != null}");
            DiagnosticLogger.Log("=== SetProgressGraphicsView SUCCESS ===");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.LogException("SetProgressGraphicsView", ex);
            throw;
        }
    }

    public void SetSourceRingGraphicsView(GraphicsView graphicsView)
    {
        _sourceRingGraphicsView = graphicsView;
    }

    public void SetDestinationRingGraphicsView(GraphicsView graphicsView)
    {
        _destinationRingGraphicsView = graphicsView;
    }

    private void UpdateRingStates()
    {
        SourceRingDrawable.IsActive = IsCopying;
        DestinationRingDrawable.IsActive = IsCopying;

        _sourceRingGraphicsView?.Invalidate();
        _destinationRingGraphicsView?.Invalidate();
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
                var sourceDir = new DirectoryInfo(SourcePath);

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

                    Application.Current?.Dispatcher.Dispatch(() => SourceFiles.Add(fileItem));
                }

                Application.Current?.Dispatcher.Dispatch(() =>
                {
                    HasFilesToCopy = SourceFiles.Count > 0;
                    AddLogMessage($"Found {SourceFiles.Count} files to copy");
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

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task StartOperationAsync()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(DestinationPath))
        {
            StatusMessage = "Please select both source and destination";
            return;
        }

        IsCopying = true;
        UpdateRingStates();
        ShowVerificationResults = false;
        StatusMessage = $"{SelectedOperationType}...";
        _currentOperationCts = new CancellationTokenSource();
        LogMessages.Clear();

        AddLogMessage($"Starting {SelectedOperationType} from {SourcePath} to {DestinationPath}");

        var progress = new Progress<FileTransferProgress>(UpdateProgress);

        try
        {
            CopyOperation? operation = null;

            switch (SelectedOperationType)
            {
                case CopyOperationType.Copy:
                    operation = await _fileOperationService.StartCopyAsync(
                        SourcePath, DestinationPath, progress, _currentOperationCts.Token);
                    break;

                case CopyOperationType.Move:
                    operation = await _fileOperationService.StartMoveAsync(
                        SourcePath, DestinationPath, progress, _currentOperationCts.Token);
                    break;

                case CopyOperationType.Mirror:
                    operation = await _fileOperationService.StartMirrorAsync(
                        SourcePath, DestinationPath, progress, _currentOperationCts.Token);
                    break;

                default:
                    throw new NotSupportedException($"Operation type {SelectedOperationType} not supported");
            }

            if (operation != null)
            {
                _currentOperationId = operation.Id;

                if (operation.Status == CopyStatus.Completed)
                {
                    StatusMessage = $"{SelectedOperationType} completed successfully";
                    AddLogMessage($"{SelectedOperationType} completed: {operation.FilesTransferred} files, {FormatBytes(operation.BytesTransferred)} transferred");
                    OverallProgress = 100;
                }
                else if (operation.Status == CopyStatus.Failed)
                {
                    StatusMessage = $"{SelectedOperationType} failed: {operation.ErrorMessage}";
                    AddLogMessage($"{SelectedOperationType} failed: {operation.ErrorMessage}");
                }
                else if (operation.Status == CopyStatus.Cancelled)
                {
                    StatusMessage = $"{SelectedOperationType} cancelled";
                    AddLogMessage($"{SelectedOperationType} cancelled by user");
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            AddLogMessage($"Error: {ex.Message}");
        }
        finally
        {
            IsCopying = false;
            UpdateRingStates();
            _currentOperationCts?.Dispose();
            _currentOperationCts = null;
        }
    }

    private bool CanStartOperation() => !IsCopying && !string.IsNullOrWhiteSpace(SourcePath) && !string.IsNullOrWhiteSpace(DestinationPath);

    [RelayCommand(CanExecute = nameof(CanStartOperation))]
    private async Task VerifyAsync()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(DestinationPath))
        {
            StatusMessage = "Please select both source and destination";
            return;
        }

        IsCopying = true;
        UpdateRingStates();
        StatusMessage = "Verifying...";
        _currentOperationCts = new CancellationTokenSource();
        LogMessages.Clear();

        AddLogMessage($"Starting verification: {SourcePath} vs {DestinationPath}");

        var progress = new Progress<FileTransferProgress>(UpdateProgress);

        try
        {
            var result = await _fileOperationService.StartVerifyAsync(
                SourcePath, DestinationPath, progress, _currentOperationCts.Token);

            VerificationResult = result;
            ShowVerificationResults = true;

            if (result.IsIdentical)
            {
                StatusMessage = "Verification complete: Directories are identical";
                AddLogMessage($"All {result.IdenticalFiles} files are identical");
            }
            else
            {
                StatusMessage = $"Verification complete: {result.DifferentFiles.Count + result.MissingFiles.Count + result.ExtraFiles.Count} differences found";
                AddLogMessage($"Found {result.DifferentFiles.Count} different, " +
                             $"{result.MissingFiles.Count} missing, " +
                             $"{result.ExtraFiles.Count} extra files");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Verification error: {ex.Message}";
            AddLogMessage($"Error: {ex.Message}");
        }
        finally
        {
            IsCopying = false;
            UpdateRingStates();
            _currentOperationCts?.Dispose();
            _currentOperationCts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelCopy))]
    private async Task CancelCopyAsync()
    {
        if (_currentOperationCts != null)
        {
            _currentOperationCts.Cancel();
            StatusMessage = "Cancelling...";
            AddLogMessage("Cancel requested");
        }
    }

    private bool CanCancelCopy() => IsCopying;

    public async Task SetSourcePathAsync(string path)
    {
        SourcePath = path;
        AddLogMessage($"Source path set: {path}");
        await ScanSourceDirectoryAsync();
    }

    private void UpdateProgress(FileTransferProgress progress)
    {
        // Ensure UI updates happen on main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentFileName = progress.FileName;
            CurrentFileProgress = progress.PercentComplete;
            CurrentSpeed = $"{FormatBytes((long)progress.CurrentSpeedBytesPerSecond)}/s";
            AverageSpeed = $"{FormatBytes((long)progress.AverageSpeedBytesPerSecond)}/s";

            // Update source file item progress
            var sourceFile = SourceFiles.FirstOrDefault(f => f.FileName == progress.FileName);
            if (sourceFile != null)
            {
                sourceFile.Progress = progress.PercentComplete;
                sourceFile.IsCopying = progress.PercentComplete < 100 && !progress.IsRetrying;
                sourceFile.IsCompleted = progress.PercentComplete >= 100;
                sourceFile.IsRetrying = progress.IsRetrying;
                sourceFile.RetryAttempt = progress.RetryAttempt;
                sourceFile.LastError = progress.LastError;
            }

            if (_currentOperationId != null)
            {
                var overallProg = _progressTracker.GetOverallProgress(_currentOperationId);
                OverallProgress = overallProg;

                // Update progress circle drawable
                ProgressDrawable.Progress = overallProg;
                ProgressDrawable.IsActive = IsCopying;
                ProgressDrawable.StatusText = IsCopying ? "TRANSFERRING" : "READY";
                _progressGraphicsView?.Invalidate();

                var eta = _progressTracker.GetEstimatedTimeRemaining(_currentOperationId);
                if (eta.HasValue)
                {
                    EstimatedTimeRemaining = FormatTimeSpan(eta.Value);
                }
            }

            if (progress.PercentComplete >= 100 && sourceFile != null)
            {
                AddLogMessage($"Completed: {progress.FileName} ({FormatBytes(progress.FileSize)})");

                // Remove from source pane
                SourceFiles.Remove(sourceFile);

                // Add to destination pane
                var destFile = new FileItem
                {
                    FileName = progress.FileName,
                    FullPath = Path.Combine(DestinationPath, progress.FileName),
                    SizeBytes = progress.FileSize,
                    FormattedSize = FormatBytes(progress.FileSize),
                    Progress = 100,
                    IsDirectory = false,
                    IsCopying = false,
                    IsCompleted = true
                };
                DestinationFiles.Add(destFile);
            }
        });
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
