using CommunityToolkit.Mvvm.ComponentModel;

namespace KopioRapido.Models;

public partial class FileItem : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private long _sizeBytes;

    [ObservableProperty]
    private string _formattedSize = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private bool _isCopying;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _isRetrying;

    [ObservableProperty]
    private int _retryAttempt;

    [ObservableProperty]
    private string? _lastError;
}
