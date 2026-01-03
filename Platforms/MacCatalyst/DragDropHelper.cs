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

        var dropDelegate = new DropInteractionDelegate(onDrop, logAction);
        var dropInteraction = new UIDropInteraction(dropDelegate);
        view.AddInteraction(dropInteraction);
        view.UserInteractionEnabled = true; // Ensure interaction is enabled

        // CRITICAL: Recursively enable drop on all child views
        // This ensures child elements (labels, buttons, etc.) don't block drops
        EnableDropOnChildViews(view, dropDelegate);

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

        var successMsg = $"[DragDropHelper] Successfully added UIDropInteraction to {element.GetType().Name} and {CountChildViews(view)} children";
        Console.WriteLine(successMsg);
        LogToFile(successMsg);
    }

    private static void EnableDropOnChildViews(UIView view, DropInteractionDelegate dropDelegate)
    {
        foreach (var subview in view.Subviews)
        {
            // Remove existing drop interactions
            foreach (var interaction in subview.Interactions)
            {
                if (interaction is UIDropInteraction)
                {
                    subview.RemoveInteraction(interaction);
                }
            }

            // Add drop interaction to this child
            var childDropInteraction = new UIDropInteraction(dropDelegate);
            subview.AddInteraction(childDropInteraction);

            // Recursively handle this child's children
            EnableDropOnChildViews(subview, dropDelegate);
        }
    }

    private static int CountChildViews(UIView view)
    {
        int count = view.Subviews.Length;
        foreach (var subview in view.Subviews)
        {
            count += CountChildViews(subview);
        }
        return count;
    }

    class DropInteractionDelegate : UIDropInteractionDelegate
    {
        private readonly Func<string, Task> _onDrop;
        private readonly Action<string>? _logAction;

        public DropInteractionDelegate(Func<string, Task> onDrop, Action<string>? logAction)
        {
            _onDrop = onDrop;
            _logAction = logAction;
            LogToFile($"[DragDropHelper] DropInteractionDelegate CREATED");
        }

        private void Log(string message)
        {
            _logAction?.Invoke(message);
            Console.WriteLine(message);
            LogToFile(message);
        }

        public override bool CanHandleSession(UIDropInteraction interaction, IUIDropSession session)
        {
            Log($"[DragDropHelper] ✅ CanHandleSession CALLED: {session.Items.Length} items");

            // Log item types
            for (int i = 0; i < session.Items.Length; i++)
            {
                var item = session.Items[i];
                Log($"[DragDropHelper]   Item[{i}]: HasItemProvider={item.ItemProvider != null}");
            }

            // Accept any session with items - we'll filter in PerformDrop
            var canHandle = session.Items.Length > 0;
            Log($"[DragDropHelper] CanHandleSession returning: {canHandle}");
            return canHandle;
        }

        public override UIDropProposal SessionDidUpdate(UIDropInteraction interaction, IUIDropSession session)
        {
            Log($"[DragDropHelper] ✅ SessionDidUpdate CALLED");

            // Always propose Copy operation for drops
            var proposal = new UIDropProposal(UIDropOperation.Copy);
            Log($"[DragDropHelper] SessionDidUpdate returning: Copy operation");
            return proposal;
        }

        public override void SessionDidEnd(UIDropInteraction interaction, IUIDropSession session)
        {
            Log($"[DragDropHelper] ✅ SessionDidEnd CALLED");
        }

        public override void SessionDidExit(UIDropInteraction interaction, IUIDropSession session)
        {
            Log($"[DragDropHelper] SessionDidExit CALLED");
        }

        public override void SessionDidEnter(UIDropInteraction interaction, IUIDropSession session)
        {
            Log($"[DragDropHelper] SessionDidEnter CALLED");
        }

        public override void PerformDrop(UIDropInteraction interaction, IUIDropSession session)
        {
            Log($"[DragDropHelper] ✅✅✅ PerformDrop CALLED with {session.Items.Length} items");

            foreach (var item in session.Items)
            {
                Log($"[DragDropHelper] Processing drag item, HasItemProvider: {item.ItemProvider != null}");

                if (item.ItemProvider == null)
                {
                    Log("[DragDropHelper] ❌ ItemProvider is null, skipping");
                    continue;
                }

                // Try multiple UTIs for maximum compatibility
                var utis = new[] { "public.file-url", "public.url", "public.data" };
                bool itemHandled = false;

                foreach (var uti in utis)
                {
                    if (itemHandled) break;

                    Log($"[DragDropHelper] Trying LoadItem with '{uti}'...");
                    item.ItemProvider.LoadItem(uti, null, (data, error) =>
                    {
                        if (error != null)
                        {
                            Log($"[DragDropHelper] Error loading with '{uti}': {error.LocalizedDescription}");
                            return;
                        }

                        if (data == null)
                        {
                            Log($"[DragDropHelper] No data returned for '{uti}'");
                            return;
                        }

                        Log($"[DragDropHelper] Got data for '{uti}': Type={data.GetType().Name}");

                        if (data is NSUrl url)
                        {
                            Log($"[DragDropHelper] Data is NSUrl: {url}, IsFileUrl={url.IsFileUrl}, Path={url.Path}");

                            string? path = null;

                            if (url.IsFileUrl)
                            {
                                path = url.Path;
                            }
                            else if (!string.IsNullOrEmpty(url.AbsoluteString))
                            {
                                // Try to extract file path from URL string
                                var urlString = url.AbsoluteString;
                                if (urlString.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                                {
                                    path = Uri.UnescapeDataString(urlString.Substring(7));
                                }
                            }

                            if (!string.IsNullOrEmpty(path))
                            {
                                Log($"[DragDropHelper] ✅ Extracted path: {path}");
                                itemHandled = true;

                                // Start accessing security-scoped resource
                                if (url.StartAccessingSecurityScopedResource())
                                {
                                    try
                                    {
                                        MainThread.BeginInvokeOnMainThread(async () =>
                                        {
                                            try
                                            {
                                                await _onDrop(path);
                                            }
                                            catch (Exception ex)
                                            {
                                                Log($"[DragDropHelper] Exception in _onDrop: {ex.Message}");
                                            }
                                        });
                                    }
                                    finally
                                    {
                                        url.StopAccessingSecurityScopedResource();
                                    }
                                }
                                else
                                {
                                    Log("[DragDropHelper] Failed to access security-scoped resource, trying anyway...");
                                    // Try anyway - might work for non-sandboxed locations
                                    MainThread.BeginInvokeOnMainThread(async () =>
                                    {
                                        try
                                        {
                                            await _onDrop(path);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log($"[DragDropHelper] Exception in _onDrop: {ex.Message}");
                                        }
                                    });
                                }
                            }
                            else
                            {
                                Log($"[DragDropHelper] Could not extract path from URL");
                            }
                        }
                        else
                        {
                            Log($"[DragDropHelper] Data is not NSUrl: {data.GetType().Name}");
                        }
                    });
                }
                
                break; // Only handle first item
            }
        }
    }
}
