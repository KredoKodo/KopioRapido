using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
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
            }
        }
        catch (Exception ex)
        {
            AddLogMessage($"Error selecting source: {ex.Message}");
        }
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

    [RelayCommand(CanExecute = nameof(CanStartCopy))]
    private async Task StartCopyAsync()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(DestinationPath))
        {
            StatusMessage = "Please select both source and destination";
            return;
        }

        IsCopying = true;
        StatusMessage = "Copying...";
        _currentOperationCts = new CancellationTokenSource();
        LogMessages.Clear();

        AddLogMessage($"Starting copy from {SourcePath} to {DestinationPath}");

        var progress = new Progress<FileTransferProgress>(UpdateProgress);

        try
        {
            var operation = await _fileOperationService.StartCopyAsync(
                SourcePath,
                DestinationPath,
                progress,
                _currentOperationCts.Token);

            _currentOperationId = operation.Id;

            if (operation.Status == CopyStatus.Completed)
            {
                StatusMessage = "Copy completed successfully";
                AddLogMessage($"Copy completed: {operation.FilesTransferred} files, {FormatBytes(operation.BytesTransferred)} transferred");
                OverallProgress = 100;
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            AddLogMessage($"Error: {ex.Message}");
        }
        finally
        {
            IsCopying = false;
            _currentOperationCts?.Dispose();
            _currentOperationCts = null;
        }
    }

    private bool CanStartCopy() => !IsCopying && !string.IsNullOrWhiteSpace(SourcePath) && !string.IsNullOrWhiteSpace(DestinationPath);

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

    public void SetSourcePath(string path)
    {
        SourcePath = path;
        AddLogMessage($"Source path set: {path}");
    }

    private void UpdateProgress(FileTransferProgress progress)
    {
        CurrentFileName = progress.FileName;
        CurrentFileProgress = progress.PercentComplete;
        CurrentSpeed = $"{FormatBytes((long)progress.CurrentSpeedBytesPerSecond)}/s";
        AverageSpeed = $"{FormatBytes((long)progress.AverageSpeedBytesPerSecond)}/s";

        if (_currentOperationId != null)
        {
            var overallProg = _progressTracker.GetOverallProgress(_currentOperationId);
            OverallProgress = overallProg;

            var eta = _progressTracker.GetEstimatedTimeRemaining(_currentOperationId);
            if (eta.HasValue)
            {
                EstimatedTimeRemaining = FormatTimeSpan(eta.Value);
            }
        }

        if (progress.PercentComplete >= 100)
        {
            AddLogMessage($"Completed: {progress.FileName} ({FormatBytes(progress.FileSize)})");
        }
    }

    private void AddLogMessage(string message)
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
