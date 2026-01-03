using UIKit;
using Foundation;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Platform;

namespace KopioRapido.Platforms.MacCatalyst;

public static class DragDropHelper
{
    private static string? _logPath = null;
    private static bool _logLocationPrinted = false;

    private static void LogToFile(string message)
    {
        try
        {
            // Initialize log path on first call
            if (_logPath == null)
            {
                var logDir = FileSystem.CacheDirectory;
                _logPath = System.IO.Path.Combine(logDir, "KopioRapido_DragDrop.log");
                
                // Clear old log on startup
                if (System.IO.File.Exists(_logPath))
                {
                    System.IO.File.Delete(_logPath);
                }
                
                // Write startup marker
                System.IO.File.AppendAllText(_logPath, $"=== LOG STARTED {DateTime.Now} ==={Environment.NewLine}");
                System.IO.File.AppendAllText(_logPath, $"Log path: {_logPath}{Environment.NewLine}");
            }
            
            System.IO.File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            
            // Also print to console where we are logging, but only once to avoid spam
            if (!_logLocationPrinted)
            {
                Console.WriteLine($"[DragDropHelper] ===== LOGGING TO FILE: {_logPath} =====");
                _logLocationPrinted = true;
            }
        }
        catch (Exception ex) 
        { 
            Console.WriteLine($"[DragDropHelper] !!!!! FAILED TO WRITE LOG: {ex.Message} !!!!!");
            Console.WriteLine($"[DragDropHelper] Exception: {ex}");
        }
    }

    public static void ConfigureDragDrop(VisualElement element, Func<string, Task> onDrop, Action<string>? logAction = null)
    {
        var msg = $"[DragDropHelper] ConfigureDragDrop CALLED for {element?.GetType().Name ?? "NULL"}";
        Console.WriteLine(msg);
        LogToFile(msg);
        
        if (element?.Handler?.PlatformView is not UIView view)
        {
            var msg2 = $"[DragDropHelper] Failed to configure: PlatformView is null or not UIView for {element?.GetType().Name}";
            logAction?.Invoke(msg2);
            Console.WriteLine(msg2);
            LogToFile(msg2);
            return;
        }
        
        var msg3 = $"[DragDropHelper] Configuring drag drop for {element.GetType().Name}";
        logAction?.Invoke(msg3);
        Console.WriteLine(msg3);
        LogToFile(msg3);
        
        // Remove existing interactions to avoid duplicates
        foreach (var interaction in view.Interactions)
        {
            if (interaction is UIDropInteraction)
            {
                view.RemoveInteraction(interaction);
            }
        }

        var dropInteraction = new UIDropInteraction(new DropInteractionDelegate(onDrop, logAction));
        view.AddInteraction(dropInteraction);
        view.UserInteractionEnabled = true; // Ensure interaction is enabled
        
        // Log view details
        var viewDetails = $"[DragDropHelper] View details: Frame={view.Frame}, Hidden={view.Hidden}, Alpha={view.Alpha}, UserInteractionEnabled={view.UserInteractionEnabled}";
        Console.WriteLine(viewDetails);
        LogToFile(viewDetails);

        // Check hierarchy
        var parent = view.Superview;
        int level = 0;
        while (parent != null && level < 10)
        {
            var parentMsg = $"[DragDropHelper] Parent[{level}]: {parent.GetType().Name}, UserInteractionEnabled={parent.UserInteractionEnabled}, Hidden={parent.Hidden}";
            Console.WriteLine(parentMsg);
            LogToFile(parentMsg);
            parent = parent.Superview;
            level++;
        }
        
        var successMsg = $"[DragDropHelper] Successfully added UIDropInteraction to {element.GetType().Name}";
        Console.WriteLine(successMsg);
        LogToFile(successMsg);
    }

    class DropInteractionDelegate : UIDropInteractionDelegate
    {
        private readonly Func<string, Task> _onDrop;
        private readonly Action<string>? _logAction;

        public DropInteractionDelegate(Func<string, Task> onDrop, Action<string>? logAction)
        {
            _onDrop = onDrop;
            _logAction = logAction;
        }

        private void Log(string message)
        {
            _logAction?.Invoke(message);
            Console.WriteLine(message);
            LogToFile(message);
        }

        public override bool CanHandleSession(UIDropInteraction interaction, IUIDropSession session)
        {
            Log($"[DragDropHelper] CanHandleSession: {session.Items.Length} items");
            // Return true if there are any items, we'll filter in PerformDrop
            return session.Items.Length > 0;
        }

        public override UIDropProposal SessionDidUpdate(UIDropInteraction interaction, IUIDropSession session)
        {
            return new UIDropProposal(UIDropOperation.Copy);
        }

        public override void PerformDrop(UIDropInteraction interaction, IUIDropSession session)
        {
            Log("[DragDropHelper] PerformDrop called");
            
            foreach (var item in session.Items)
            {
                Log($"[DragDropHelper] Processing drag item, HasItemProvider: {item.ItemProvider != null}");
                
                if (item.ItemProvider == null)
                {
                    Log("[DragDropHelper] ItemProvider is null, skipping");
                    continue;
                }
                
                item.ItemProvider.LoadItem("public.file-url", null, (data, error) =>
                {
                    if (error != null)
                    {
                        Log($"[DragDropHelper] Error loading item: {error.LocalizedDescription}");
                        return;
                    }

                    if (data is NSUrl url && url.IsFileUrl)
                    {
                        Log($"[DragDropHelper] Got file URL: {url.Path}");
                        
                        // Start accessing security-scoped resource
                        if (url.StartAccessingSecurityScopedResource())
                        {
                            try
                            {
                                var path = url.Path;
                                if (!string.IsNullOrEmpty(path))
                                {
                                    MainThread.BeginInvokeOnMainThread(async () =>
                                    {
                                        await _onDrop(path);
                                    });
                                }
                            }
                            finally
                            {
                                url.StopAccessingSecurityScopedResource();
                            }
                        }
                        else
                        {
                            Log("[DragDropHelper] Failed to access security-scoped resource");
                        }
                    }
                    else
                    {
                        Log($"[DragDropHelper] Data is not NSUrl or not file URL: {data?.GetType().Name}");
                    }
                });
                
                break; // Only handle first item
            }
        }
    }
}
