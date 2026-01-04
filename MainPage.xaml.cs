using KopioRapido.ViewModels;

#if WINDOWS
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
#endif

#if IOS || MACCATALYST
using UIKit;
using Foundation;
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

            DiagnosticLogger.Log("=== MainPage constructor SUCCESS ===");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.LogException("MainPage constructor", ex);
            throw;
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

#if WINDOWS
        // Set up native Windows drag-drop for the Border controls
        if (Handler != null)
        {
            ConfigureWindowsDragDrop();
        }
#endif
    }

#if WINDOWS
    private void ConfigureWindowsDragDrop()
    {
        try
        {
            ViewModel.AddLogMessage("🪟 Setting up Windows native drag-drop...");

            // Configure drag-drop for Source border
            Platforms.Windows.DragDropHelper.ConfigureDragDrop(
                SourceBorder,
                async (path) => await HandleDropAsync(path, isSource: true)
            );

            // Configure drag-drop for Destination border
            Platforms.Windows.DragDropHelper.ConfigureDragDrop(
                DestinationBorder,
                async (path) => await HandleDropAsync(path, isSource: false)
            );

            ViewModel.AddLogMessage("✅ Windows drag-drop configured successfully");
        }
        catch (Exception ex)
        {
            ViewModel.AddLogMessage($"❌ Failed to configure Windows drag-drop: {ex.Message}");
        }
    }
#endif

    private void OnCloseClicked(object sender, EventArgs e)
    {
        Application.Current?.Quit();
    }

    private void OnMinimizeClicked(object sender, EventArgs e)
    {
#if WINDOWS
        var window = this.Window;
        if (window == null) return;

        var nativeWindow = window.Handler.PlatformView;
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
#endif
    }

    private void OnMaximizeRestoreClicked(object sender, EventArgs e)
    {
#if WINDOWS
        var window = this.Window;
        if (window == null) return;

        var nativeWindow = window.Handler.PlatformView;
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            if (presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
            {
                presenter.Restore();
            }
            else
            {
                presenter.Maximize();
            }
        }
#endif
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

    // MAUI DropGestureRecognizer handlers
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Accept the drag operation
        e.AcceptedOperation = Microsoft.Maui.Controls.DataPackageOperation.Copy;
    }

    private async void OnSourceDrop(object? sender, DropEventArgs e)
    {
        ViewModel.AddLogMessage("🎯 Source drop event fired!");
        var path = await GetDroppedFilePathAsync(e);
        if (!string.IsNullOrEmpty(path))
        {
            await HandleDropAsync(path, isSource: true);
        }
    }

    private async void OnDestinationDrop(object? sender, DropEventArgs e)
    {
        ViewModel.AddLogMessage("🎯 Destination drop event fired!");
        var path = await GetDroppedFilePathAsync(e);
        if (!string.IsNullOrEmpty(path))
        {
            await HandleDropAsync(path, isSource: false);
        }
    }

    private async Task<string?> GetDroppedFilePathAsync(DropEventArgs e)
    {
        try
        {
#if IOS || MACCATALYST
            ViewModel.AddLogMessage("📱 MacCatalyst: Accessing drop session...");
            var session = e.PlatformArgs?.DropSession;
            if (session == null)
            {
                ViewModel.AddLogMessage("❌ DropSession is null");
                return null;
            }

            ViewModel.AddLogMessage($"📦 Session has {session.Items.Length} items");

            foreach (var item in session.Items)
            {
                ViewModel.AddLogMessage($"📄 Processing item with {item.ItemProvider.RegisteredTypeIdentifiers.Length} type identifiers");

                var typeIds = item.ItemProvider.RegisteredTypeIdentifiers.ToList();
                foreach (var typeId in typeIds)
                {
                    ViewModel.AddLogMessage($"   - Type: {typeId}");
                }

                var result = await LoadItemAsync(item.ItemProvider, typeIds);
                if (result != null)
                {
                    ViewModel.AddLogMessage($"✅ Got file path: {result}");
                    return result;
                }
            }

            ViewModel.AddLogMessage("❌ No file path extracted");
            return null;

            static async Task<string?> LoadItemAsync(
                NSItemProvider itemProvider, List<string> typeIdentifiers)
            {
                if (typeIdentifiers == null || typeIdentifiers.Count == 0)
                    return null;

                var typeIdent = typeIdentifiers.First();
                if (itemProvider.HasItemConformingTo(typeIdent))
                {
                    try
                    {
                        var loadResult = await itemProvider.LoadInPlaceFileRepresentationAsync(typeIdent);
                        if (loadResult?.FileUrl?.Path != null)
                        {
                            return loadResult.FileUrl.Path;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading {typeIdent}: {ex.Message}");
                    }
                }
                typeIdentifiers.Remove(typeIdent);
                return await LoadItemAsync(itemProvider, typeIdentifiers);
            }
#elif WINDOWS
            ViewModel.AddLogMessage("🪟 Windows: Accessing DataView...");
            if (e.Data?.Properties?.ContainsKey("Files") == true)
            {
                var files = e.Data.Properties["Files"] as IEnumerable<IStorageItem>;
                var firstFile = files?.FirstOrDefault();
                if (firstFile != null)
                {
                    ViewModel.AddLogMessage($"✅ Got file path: {firstFile.Path}");
                    return firstFile.Path;
                }
            }
            ViewModel.AddLogMessage("❌ No files in DataView");
            return null;
#else
            ViewModel.AddLogMessage("❌ Platform not supported");
            return null;
#endif
        }
        catch (Exception ex)
        {
            ViewModel.AddLogMessage($"❌ Exception in GetDroppedFilePathAsync: {ex.Message}");
            return null;
        }
    }

    private async Task HandleDropAsync(string path, bool isSource)
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
            ViewModel.AddLogMessage($"📂 Processing path: {path}");

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
            ViewModel.AddLogMessage($"❌ Drop error: {ex.Message}");
            await DisplayAlertAsync("Error",
                $"Failed to process dropped item: {ex.Message}",
                "OK");
        }
    }
}
