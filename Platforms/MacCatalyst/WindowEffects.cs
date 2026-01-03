using Foundation;
using UIKit;

namespace KopioRapido.Platforms.MacCatalyst;

public static class WindowEffects
{
    public static void ApplyFrostedGlassEffect(Microsoft.Maui.Controls.Window window)
    {
        // For Mac Catalyst, we'll use a translucent background
        // Full NSVisualEffectView support requires AppKit which isn't directly available in Catalyst

        var nativeWindow = window.Handler?.PlatformView as UIWindow;
        if (nativeWindow == null)
            return;

        // Set the window to be translucent
        nativeWindow.BackgroundColor = UIColor.Clear;
        nativeWindow.Opaque = false;

        // If the root view controller has a view, make it translucent
        if (nativeWindow.RootViewController?.View != null)
        {
            var rootView = nativeWindow.RootViewController.View;

            // Create a blur effect view
            var blurEffect = UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemUltraThinMaterialDark);
            var blurEffectView = new UIVisualEffectView(blurEffect)
            {
                Frame = rootView.Bounds,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
            };

            // Insert the blur view at the back
            rootView.InsertSubview(blurEffectView, 0);
            rootView.BackgroundColor = UIColor.Clear;
        }
    }
}
