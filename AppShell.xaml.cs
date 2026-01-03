namespace KopioRapido;

public partial class AppShell : Shell
{
	public AppShell()
	{
		DiagnosticLogger.Log("=== AppShell constructor START ===");
		try
		{
			DiagnosticLogger.Log("Calling InitializeComponent...");
			InitializeComponent();
			DiagnosticLogger.Log("=== AppShell constructor SUCCESS ===");
		}
		catch (Exception ex)
		{
			DiagnosticLogger.LogException("AppShell constructor", ex);
			throw;
		}
	}
}
