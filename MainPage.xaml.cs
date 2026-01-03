using KopioRapido.ViewModels;

#if WINDOWS
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
#endif

namespace KopioRapido;

public partial class MainPage : ContentPage
{
    private MainViewModel ViewModel => (MainViewModel)BindingContext;

    public MainPage(MainViewModel viewModel)
    {
        DiagnosticLogger.Log("=== MainPage constructor START ===");
        try
        {
            DiagnosticLogger.Log("Calling InitializeComponent...");
            InitializeComponent();

            DiagnosticLogger.Log("Setting BindingContext...");
            BindingContext = viewModel;

            DiagnosticLogger.Log("Wiring up GraphicsView to ViewModel...");
            // Wire up the GraphicsView to the ViewModel (progress circle only)
            // viewModel.SetProgressGraphicsView(ProgressGraphicsView);

#if WINDOWS || MACCATALYST
            DiagnosticLogger.Log("Registering Loaded event for drag-drop...");
            // Configure native drag-drop after the page is loaded
            Loaded += OnPageLoaded;
#endif

            DiagnosticLogger.Log("=== MainPage constructor SUCCESS ===");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.LogException("MainPage constructor", ex);
            throw;
        }
    }

#if WINDOWS || MACCATALYST
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        DiagnosticLogger.Log("=== OnPageLoaded START ===");
        try
        {
            DiagnosticLogger.Log("Finding SourceBorder...");
            // Find the Border elements by name and configure native drag-drop
            if (this.FindByName("SourceBorder") is Border sourceBorder)
            {
                DiagnosticLogger.Log("Configuring drag-drop for SourceBorder...");
#if WINDOWS
                Platforms.Windows.DragDropHelper.ConfigureDragDrop(sourceBorder, async path =>
                {
                    await HandleNativeDrop(path, isSource: true);
                });
#elif MACCATALYST
                Platforms.MacCatalyst.DragDropHelper.ConfigureDragDrop(sourceBorder, async path =>
                {
                    await HandleNativeDrop(path, isSource: true);
                }, ViewModel.AddLogMessage);
#endif
            }
            else
            {
                DiagnosticLogger.Log("WARNING: SourceBorder not found!");
            }

            DiagnosticLogger.Log("Finding DestinationBorder...");
            if (this.FindByName("DestinationBorder") is Border destinationBorder)
            {
                DiagnosticLogger.Log("Configuring drag-drop for DestinationBorder...");
#if WINDOWS
                Platforms.Windows.DragDropHelper.ConfigureDragDrop(destinationBorder, async path =>
                {
                    await HandleNativeDrop(path, isSource: false);
                });
#elif MACCATALYST
                Platforms.MacCatalyst.DragDropHelper.ConfigureDragDrop(destinationBorder, async path =>
                {
                    await HandleNativeDrop(path, isSource: false);
                }, ViewModel.AddLogMessage);
#endif
            }
            else
            {
                DiagnosticLogger.Log("WARNING: DestinationBorder not found!");
            }

            DiagnosticLogger.Log("=== OnPageLoaded SUCCESS ===");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.LogException("OnPageLoaded", ex);
        }
    }

    private async Task HandleNativeDrop(string path, bool isSource)
    {
        if (ViewModel.IsCopying)
        {
            await DisplayAlertAsync("Operation in Progress",
                "Cannot change paths while copying is in progress.",
                "OK");
            return;
        }

        try
        {
            ViewModel.AddLogMessage($"Received drop: {path}");

            // Validate it's a directory
            if (Directory.Exists(path))
            {
                if (isSource)
                {
                    await ViewModel.SetSourcePathAsync(path);
                    ViewModel.AddLogMessage($"✓ Source folder set: {path}");
                }
                else
                {
                    ViewModel.DestinationPath = path;
                    ViewModel.AddLogMessage($"✓ Destination folder set: {path}");
                }
            }
            else if (File.Exists(path))
            {
                // If it's a file, use its parent directory
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    if (isSource)
                    {
                        await ViewModel.SetSourcePathAsync(directory);
                    }
                    else
                    {
                        ViewModel.DestinationPath = directory;
                    }
                    ViewModel.AddLogMessage($"✓ Using parent folder: {directory}");
                }
            }
            else
            {
                await DisplayAlertAsync("Invalid Path",
                    $"The path does not exist: {path}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            ViewModel.AddLogMessage($"Drop error: {ex.Message}");
            await DisplayAlertAsync("Error",
                $"Failed to process dropped item: {ex.Message}",
                "OK");
        }
    }
#endif

    private void OnCloseClicked(object sender, EventArgs e)
    {
        Application.Current?.Quit();
    }

    private void OnWindowDrag(object sender, TappedEventArgs e)
    {
        // Placeholder for drag logic if needed, but Pan is better
    }

#if WINDOWS
    private Windows.Graphics.PointInt32 _windowStartPosition;
#endif

    private void OnWindowPan(object sender, PanUpdatedEventArgs e)
    {
#if WINDOWS
        var window = this.Window;
        if (window == null) return;

        var nativeWindow = window.Handler.PlatformView;
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        if (e.StatusType == GestureStatus.Started)
        {
            _windowStartPosition = appWindow.Position;
        }
        else if (e.StatusType == GestureStatus.Running)
        {
            // Apply scaling factor if needed, but usually TotalX/Y matches screen pixels in MAUI on Windows?
            // Actually, MAUI coordinates are DIPs, Windows Windowing is pixels.
            // We might need to multiply by density.
            var density = DeviceDisplay.MainDisplayInfo.Density;

            appWindow.Move(new Windows.Graphics.PointInt32(
                _windowStartPosition.X + (int)(e.TotalX * density),
                _windowStartPosition.Y + (int)(e.TotalY * density)));
        }
#endif
    }

    private void OnOperationChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0)
        {
            var operationType = picker.Items[picker.SelectedIndex];
            ViewModel.SelectOperationCommand.Execute(operationType);
        }
    }
}
