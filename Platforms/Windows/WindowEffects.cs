using Microsoft.Maui.Platform;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using WinRT;

namespace KopioRapido.Platforms.Windows;

public static class WindowEffects
{
    public static void ApplyAcrylicEffect(Microsoft.Maui.Controls.Window window)
    {
        var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (nativeWindow == null)
            return;

        // Try to set Mica backdrop (Windows 11 style)
        if (MicaController.IsSupported())
        {
            var micaController = new MicaController
            {
                Kind = MicaKind.Base
            };

            micaController.AddSystemBackdropTarget(nativeWindow.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            micaController.SetSystemBackdropConfiguration(new SystemBackdropConfiguration());
        }
        // Fallback to Acrylic (Windows 10 style)
        else if (DesktopAcrylicController.IsSupported())
        {
            var acrylicController = new DesktopAcrylicController
            {
                Kind = DesktopAcrylicKind.Base
            };

            acrylicController.AddSystemBackdropTarget(nativeWindow.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            acrylicController.SetSystemBackdropConfiguration(new SystemBackdropConfiguration());
        }

        // Make the window background transparent so backdrop shows through
        if (nativeWindow.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = ElementTheme.Dark;
        }
    }
}
