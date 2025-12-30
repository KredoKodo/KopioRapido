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
        InitializeComponent();
        BindingContext = viewModel;

#if WINDOWS
        // Configure native Windows drag-drop after the page is loaded
        Loaded += OnPageLoaded;
#endif
    }

#if WINDOWS
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Find the Border elements by name and configure native drag-drop
        if (this.FindByName("SourceBorder") is Border sourceBorder)
        {
            Platforms.Windows.DragDropHelper.ConfigureDragDrop(sourceBorder, async path =>
            {
                await HandleNativeDrop(path, isSource: true);
            });
        }

        if (this.FindByName("DestinationBorder") is Border destinationBorder)
        {
            Platforms.Windows.DragDropHelper.ConfigureDragDrop(destinationBorder, async path =>
            {
                await HandleNativeDrop(path, isSource: false);
            });
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
            ViewModel.AddLogMessage($"✓ Received drop: {path}");

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
                    await DisplayAlertAsync("File Dropped",
                        $"Using parent folder: {directory}",
                        "OK");

                    if (isSource)
                    {
                        await ViewModel.SetSourcePathAsync(directory);
                    }
                    else
                    {
                        ViewModel.DestinationPath = directory;
                    }
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

    // Helper method to wrap DisplayAlert as DisplayAlertAsync
    private Task DisplayAlertAsync(string title, string message, string cancel)
    {
        return DisplayAlert(title, message, cancel);
    }

    // Source drop zone handlers
    private void OnSourceDragOver(object? sender, DragEventArgs e)
    {
        if (sender is Border border && !ViewModel.IsCopying)
        {
            // Accept the drag operation
            e.AcceptedOperation = Microsoft.Maui.Controls.DataPackageOperation.Copy;

            // Visual feedback - highlight border
            border.Stroke = Colors.Green;
            border.StrokeThickness = 3;
        }
    }

    private void OnSourceDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            // Reset visual feedback to original blue
            border.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#60A5FA")
                : Color.FromArgb("#0078D4");
            border.StrokeThickness = 2;
        }
    }

    private async void OnSourceDrop(object? sender, DropEventArgs e)
    {
        if (sender is Border border)
        {
            // Reset visual feedback to original blue
            border.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#60A5FA")
                : Color.FromArgb("#0078D4");
            border.StrokeThickness = 2;

            // Process dropped files
            await HandleDropAsync(e, isSource: true);
        }
    }

    // Destination drop zone handlers
    private void OnDestinationDragOver(object? sender, DragEventArgs e)
    {
        if (sender is Border border && !ViewModel.IsCopying)
        {
            // Accept the drag operation
            e.AcceptedOperation = Microsoft.Maui.Controls.DataPackageOperation.Copy;

            // Visual feedback - highlight border
            border.Stroke = Colors.Blue;
            border.StrokeThickness = 3;
        }
    }

    private void OnDestinationDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            // Reset visual feedback to original green
            border.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#34D399")
                : Color.FromArgb("#10B981");
            border.StrokeThickness = 2;
        }
    }

    private async void OnDestinationDrop(object? sender, DropEventArgs e)
    {
        if (sender is Border border)
        {
            // Reset visual feedback to original green
            border.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#34D399")
                : Color.FromArgb("#10B981");
            border.StrokeThickness = 2;

            // Process dropped files
            await HandleDropAsync(e, isSource: false);
        }
    }

    private async Task HandleDropAsync(DropEventArgs e, bool isSource)
    {
        if (ViewModel.IsCopying)
        {
            await DisplayAlertAsync("Operation in Progress",
                "Cannot change paths while copying is in progress.",
                "OK");
            return;
        }

        var debugLog = new System.Text.StringBuilder();
        debugLog.AppendLine("=== DROP EVENT DEBUG LOG ===");
        debugLog.AppendLine($"Time: {DateTime.Now}");
        debugLog.AppendLine();

        try
        {
            ViewModel.AddLogMessage("=== DROP EVENT START ===");

            // Log ALL available properties with detailed types
            ViewModel.AddLogMessage($"Property count: {e.Data.Properties.Count}");
            debugLog.AppendLine($"Property count: {e.Data.Properties.Count}");

            foreach (var key in e.Data.Properties.Keys)
            {
                var value = e.Data.Properties[key];
                var typeName = value?.GetType().FullName ?? "null";
                var msg = $"[{key}] = {typeName}";
                ViewModel.AddLogMessage(msg);
                debugLog.AppendLine(msg);

                // Try to show the value
                if (value != null)
                {
                    try
                    {
                        if (value is string str)
                        {
                            var valMsg = $"  String value: '{str}'";
                            ViewModel.AddLogMessage(valMsg);
                            debugLog.AppendLine(valMsg);
                        }
                        else if (value is System.Collections.IEnumerable enumerable && !(value is string))
                        {
                            var items = enumerable.Cast<object>().Take(5).ToList();
                            var collMsg = $"  Collection with {items.Count} items";
                            ViewModel.AddLogMessage(collMsg);
                            debugLog.AppendLine(collMsg);
                            foreach (var item in items)
                            {
                                var itemMsg = $"    - {item?.GetType().FullName}: {item}";
                                ViewModel.AddLogMessage(itemMsg);
                                debugLog.AppendLine(itemMsg);
                            }
                        }
                        else
                        {
                            var valMsg = $"  Value: {value}";
                            ViewModel.AddLogMessage(valMsg);
                            debugLog.AppendLine(valMsg);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errMsg = $"  Error reading value: {ex.Message}";
                        ViewModel.AddLogMessage(errMsg);
                        debugLog.AppendLine(errMsg);
                    }
                }
            }

            string? selectedPath = null;

#if WINDOWS
            // Windows-specific: Use native Windows APIs to get dropped files
            ViewModel.AddLogMessage("Attempting to get Windows StorageItems...");
            try
            {
                // e.Data is the DataPackageView in MAUI
                var view = e.Data;
                debugLog.AppendLine($"DataPackageView type: {view?.GetType().FullName}");
                ViewModel.AddLogMessage($"DataPackageView type: {view?.GetType().FullName}");

                if (view != null)
                {
                    // Check if we can get storage items using reflection or direct access
                    var viewType = view.GetType();
                    debugLog.AppendLine($"Checking for available methods and properties on DataPackageView");

                    // Try to find a method to get the platform view
                    var platformViewProp = viewType.GetProperty("PlatformView");
                    if (platformViewProp != null)
                    {
                        debugLog.AppendLine("Found PlatformView property");
                        ViewModel.AddLogMessage("Found PlatformView property");

                        var platformView = platformViewProp.GetValue(view);
                        if (platformView is Windows.ApplicationModel.DataTransfer.DataPackageView winView)
                        {
                            debugLog.AppendLine("Successfully cast to Windows DataPackageView");
                            ViewModel.AddLogMessage("Successfully cast to Windows DataPackageView");

                            if (winView.Contains(StandardDataFormats.StorageItems))
                            {
                                debugLog.AppendLine("Contains StorageItems format");
                                ViewModel.AddLogMessage("Contains StorageItems format");

                                var storageItems = await winView.GetStorageItemsAsync();
                                debugLog.AppendLine($"Got {storageItems?.Count ?? 0} storage items");
                                ViewModel.AddLogMessage($"Retrieved {storageItems?.Count ?? 0} storage items");

                                if (storageItems != null && storageItems.Count > 0)
                                {
                                    var firstItem = storageItems[0];
                                    selectedPath = firstItem.Path;
                                    debugLog.AppendLine($"First item path: {selectedPath}");
                                    ViewModel.AddLogMessage($"✓ Got path from StorageItems: {selectedPath}");
                                }
                            }
                            else
                            {
                                debugLog.AppendLine("Does NOT contain StorageItems format");
                                ViewModel.AddLogMessage("Does NOT contain StorageItems format");

                                // Log all available formats
                                var formats = winView.AvailableFormats;
                                debugLog.AppendLine($"Available formats count: {formats?.Count ?? 0}");
                                if (formats != null)
                                {
                                    foreach (var format in formats)
                                    {
                                        debugLog.AppendLine($"  - Format: {format}");
                                        ViewModel.AddLogMessage($"  Available format: {format}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            debugLog.AppendLine($"PlatformView is type: {platformView?.GetType().FullName ?? "null"}");
                            ViewModel.AddLogMessage($"PlatformView is not Windows DataPackageView, it's: {platformView?.GetType().FullName ?? "null"}");
                        }
                    }
                    else
                    {
                        debugLog.AppendLine("No PlatformView property found");
                        ViewModel.AddLogMessage("No PlatformView property found on DataPackageView");
                    }
                }
                else
                {
                    debugLog.AppendLine("DataPackageView is null!");
                    ViewModel.AddLogMessage("DataPackageView is null!");
                }
            }
            catch (Exception ex)
            {
                var errMsg = $"Error getting Windows storage items: {ex.GetType().Name}: {ex.Message}";
                debugLog.AppendLine(errMsg);
                debugLog.AppendLine($"Stack: {ex.StackTrace}");
                ViewModel.AddLogMessage(errMsg);
            }
#endif

            // Fallback: Try standard MAUI properties
            if (selectedPath == null && e.Data.Properties.Count > 0)
            {
                // Try FileNames
                if (e.Data.Properties.ContainsKey("FileNames"))
                {
                    var files = e.Data.Properties["FileNames"];
                    if (files is IEnumerable<string> stringList)
                    {
                        selectedPath = stringList.FirstOrDefault();
                    }
                }

                // Try Text
                if (selectedPath == null && e.Data.Properties.ContainsKey("Text"))
                {
                    var text = await e.Data.GetTextAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        selectedPath = text.Trim();
                    }
                }
            }

            ViewModel.AddLogMessage($"Final selected path: {selectedPath ?? "NULL"}");
            ViewModel.AddLogMessage("=== DROP EVENT END ===");
            debugLog.AppendLine();
            debugLog.AppendLine($"Final selected path: {selectedPath ?? "NULL"}");
            debugLog.AppendLine("=== DROP EVENT END ===");

            // Write debug log to file
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var logFilePath = Path.Combine(desktopPath, $"KopioRapido_Drop_Debug_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                await File.WriteAllTextAsync(logFilePath, debugLog.ToString());
                ViewModel.AddLogMessage($"Debug log written to: {logFilePath}");
            }
            catch (Exception logEx)
            {
                ViewModel.AddLogMessage($"Failed to write debug log: {logEx.Message}");
            }

            if (!string.IsNullOrEmpty(selectedPath))
            {
                ViewModel.AddLogMessage($"Selected path: {selectedPath}");

                // Validate it's a directory
                if (Directory.Exists(selectedPath))
                {
                    if (isSource)
                    {
                        await ViewModel.SetSourcePathAsync(selectedPath);
                        ViewModel.AddLogMessage($"✓ Source folder set: {selectedPath}");
                    }
                    else
                    {
                        ViewModel.DestinationPath = selectedPath;
                        ViewModel.AddLogMessage($"✓ Destination folder set: {selectedPath}");
                    }
                }
                else if (File.Exists(selectedPath))
                {
                    // If it's a file, use its parent directory
                    var directory = Path.GetDirectoryName(selectedPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        await DisplayAlert("File Dropped",
                            $"Using parent folder: {directory}",
                            "OK");

                        if (isSource)
                        {
                            await ViewModel.SetSourcePathAsync(directory);
                        }
                        else
                        {
                            ViewModel.DestinationPath = directory;
                        }
                    }
                }
                else
                {
                    await DisplayAlert("Invalid Path",
                        $"The path does not exist: {selectedPath}",
                        "OK");
                }
            }
            else
            {
                await DisplayAlert("Drop Not Supported",
                    "Could not extract file path from drop operation. Try using the folder picker button instead.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            ViewModel.AddLogMessage($"Drop error: {ex.Message}");
            debugLog.AppendLine();
            debugLog.AppendLine($"EXCEPTION: {ex.Message}");
            debugLog.AppendLine($"Stack trace: {ex.StackTrace}");

            // Write debug log to file even on error
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var logFilePath = Path.Combine(desktopPath, $"KopioRapido_Drop_Debug_ERROR_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                await File.WriteAllTextAsync(logFilePath, debugLog.ToString());
                ViewModel.AddLogMessage($"Error debug log written to: {logFilePath}");
            }
            catch { }

            await DisplayAlert("Error",
                $"Failed to process dropped item: {ex.Message}\n\nStack: {ex.StackTrace}",
                "OK");
        }
    }
}
