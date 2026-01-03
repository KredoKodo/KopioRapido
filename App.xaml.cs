using Microsoft.Extensions.DependencyInjection;
using KopioRapido.Helpers;

namespace KopioRapido;

public partial class App : Application
{
	public App()
	{
		DiagnosticLogger.Log("=== App constructor START ===");
		try
		{
			DiagnosticLogger.Log("Calling InitializeComponent...");
			InitializeComponent();
			DiagnosticLogger.Log("=== App constructor SUCCESS ===");
		}
		catch (Exception ex)
		{
			DiagnosticLogger.LogException("App constructor", ex);
			throw;
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		DiagnosticLogger.Log("=== CreateWindow START ===");
		try
		{
			DiagnosticLogger.Log("Creating AppShell...");
			var shell = new AppShell();

			DiagnosticLogger.Log("Creating Window with AppShell...");
			var window = new Window(shell);

			// Configure Window: Adaptive sizing with golden ratio and persistence
			window.Created += (s, e) =>
			{
#if WINDOWS
				// Get the native window handle
				var nativeWindow = window.Handler.PlatformView;
				var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
				var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
				var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
				var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);

				// Calculate adaptive size based on screen dimensions
				int screenWidth = displayArea.WorkArea.Width;
				int screenHeight = displayArea.WorkArea.Height;

				// Try to restore saved size, or calculate default
				var (savedWidth, savedHeight) = WindowPreferences.GetSavedSize();
				int windowWidth, windowHeight;

				if (savedWidth.HasValue && savedHeight.HasValue)
				{
					(windowWidth, windowHeight) = WindowSizer.ValidateSavedSize(savedWidth.Value, savedHeight.Value, screenWidth, screenHeight);
				}
				else
				{
					(windowWidth, windowHeight) = WindowSizer.CalculateDefaultSize(screenWidth, screenHeight);
				}

				// Set window size
				appWindow.Resize(new Windows.Graphics.SizeInt32(windowWidth, windowHeight));

				// Try to restore saved position, or center
				var (savedX, savedY) = WindowPreferences.GetSavedPosition();
				int windowX, windowY;

				if (savedX.HasValue && savedY.HasValue)
				{
					(windowX, windowY) = WindowSizer.ValidateSavedPosition(savedX.Value, savedY.Value, windowWidth, windowHeight, screenWidth, screenHeight);
				}
				else
				{
					(windowX, windowY) = WindowSizer.CenterWindow(windowWidth, windowHeight, screenWidth, screenHeight);
				}

				appWindow.Move(new Windows.Graphics.PointInt32(windowX, windowY));

				// Configure window properties
				if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
				{
					presenter.SetBorderAndTitleBar(false, false);
					presenter.IsResizable = true; // Allow resizing
					presenter.IsMaximizable = true;
					presenter.IsMinimizable = true;
				}

				// Save size on changes
				appWindow.Changed += (sender, args) =>
				{
					if (args.DidSizeChange)
					{
						WindowPreferences.SaveSize(appWindow.Size.Width, appWindow.Size.Height);
					}
					if (args.DidPositionChange)
					{
						WindowPreferences.SavePosition(appWindow.Position.X, appWindow.Position.Y);
					}
				};
#elif MACCATALYST
				// Set minimum size constraints only
				window.MinimumWidth = 750;
				window.MinimumHeight = 550;

				// Calculate adaptive size based on screen dimensions (approximate)
				// Note: DisplayInformation not available on macOS, using defaults for common resolutions
				int screenWidth = 1920; // Default assumption
				int screenHeight = 1080;

				// Try to restore saved size, or calculate default
				var (savedWidth, savedHeight) = WindowPreferences.GetSavedSize();
				int windowWidth, windowHeight;

				if (savedWidth.HasValue && savedHeight.HasValue)
				{
					(windowWidth, windowHeight) = WindowSizer.ValidateSavedSize(savedWidth.Value, savedHeight.Value, screenWidth, screenHeight);
				}
				else
				{
					(windowWidth, windowHeight) = WindowSizer.CalculateDefaultSize(screenWidth, screenHeight);
				}

				window.Width = windowWidth;
				window.Height = windowHeight;

				// Hide title bar
				if (window.Handler.PlatformView is UIKit.UIWindow uiWindow && 
					uiWindow.WindowScene != null && 
					uiWindow.WindowScene.Titlebar != null)
				{
					uiWindow.WindowScene.Titlebar.TitleVisibility = UIKit.UITitlebarTitleVisibility.Hidden;
					uiWindow.WindowScene.Titlebar.Toolbar = null;
				}

				// Note: macOS window size/position change events would need native code to implement
				// For now, size is saved on app shutdown via OnSleep() method
#endif
			};

			DiagnosticLogger.Log("=== CreateWindow SUCCESS ===");
			return window;
		}
		catch (Exception ex)
		{
			DiagnosticLogger.LogException("CreateWindow", ex);
			throw;
		}
	}

	protected override void OnSleep()
	{
		// Save window state on macOS when app goes to background
#if MACCATALYST
		try
		{
			var window = Windows.FirstOrDefault();
			if (window != null)
			{
				WindowPreferences.SaveSize((int)window.Width, (int)window.Height);
			}
		}
		catch (Exception ex)
		{
			DiagnosticLogger.LogException("OnSleep - SaveWindowState", ex);
		}
#endif
		base.OnSleep();
	}
}