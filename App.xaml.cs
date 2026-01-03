using Microsoft.Extensions.DependencyInjection;

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

			// Configure Window: Fixed Size (600x800) and Borderless
			window.Created += (s, e) =>
			{
#if WINDOWS
				// Get the native window handle
				var nativeWindow = window.Handler.PlatformView;
				var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
				var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
				var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

				// Set fixed size to 750x550
				appWindow.Resize(new Windows.Graphics.SizeInt32(750, 550));

				// Center on screen
				var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
				var centeredPosition = new Windows.Graphics.PointInt32(
					(displayArea.WorkArea.Width - 750) / 2,
					(displayArea.WorkArea.Height - 550) / 2);
				appWindow.Move(centeredPosition);

				// Remove borders and title bar (Frameless)
				if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
				{
					presenter.SetBorderAndTitleBar(false, false);
					presenter.IsResizable = false;
					presenter.IsMaximizable = false;
				}
#elif MACCATALYST
				// Set fixed size constraints
				window.MinimumWidth = 750;
				window.MaximumWidth = 750;
				window.MinimumHeight = 550;
				window.MaximumHeight = 550;
				
				// Hide title bar
				if (window.Handler.PlatformView is UIKit.UIWindow uiWindow && 
					uiWindow.WindowScene != null && 
					uiWindow.WindowScene.Titlebar != null)
				{
					uiWindow.WindowScene.Titlebar.TitleVisibility = UIKit.UITitlebarTitleVisibility.Hidden;
					uiWindow.WindowScene.Titlebar.Toolbar = null;
				}
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
}