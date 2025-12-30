using Microsoft.Maui.Controls;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KopioRapido.Platforms.Windows;

public static class DragDropHelper
{
    public static void ConfigureDragDrop(Border border, Func<string, Task> onFolderDropped)
    {
        if (border?.Handler?.PlatformView is not FrameworkElement nativeElement)
            return;

        nativeElement.AllowDrop = true;

        nativeElement.DragOver += (s, e) =>
        {
            e.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Drop folder here";
            e.Handled = true;
        };

        nativeElement.Drop += async (s, e) =>
        {
            try
            {
                var deferral = e.GetDeferral();

                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items.Count > 0)
                    {
                        var firstItem = items[0];
                        var path = firstItem.Path;

                        if (!string.IsNullOrEmpty(path))
                        {
                            await onFolderDropped(path);
                        }
                    }
                }

                deferral.Complete();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Drop error: {ex.Message}");
            }
        };
    }
}
