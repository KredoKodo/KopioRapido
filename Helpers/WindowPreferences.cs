namespace KopioRapido.Helpers;

public static class WindowPreferences
{
    private const string WidthKey = "WindowWidth";
    private const string HeightKey = "WindowHeight";
    private const string XKey = "WindowX";
    private const string YKey = "WindowY";
    private const string IsMaximizedKey = "WindowIsMaximized";

    public static void SaveSize(int width, int height)
    {
        Preferences.Default.Set(WidthKey, width);
        Preferences.Default.Set(HeightKey, height);
    }

    public static void SavePosition(int x, int y)
    {
        Preferences.Default.Set(XKey, x);
        Preferences.Default.Set(YKey, y);
    }

    public static void SaveMaximizedState(bool isMaximized)
    {
        Preferences.Default.Set(IsMaximizedKey, isMaximized);
    }

    public static (int? width, int? height) GetSavedSize()
    {
        if (Preferences.Default.ContainsKey(WidthKey) && Preferences.Default.ContainsKey(HeightKey))
        {
            int width = Preferences.Default.Get(WidthKey, 0);
            int height = Preferences.Default.Get(HeightKey, 0);
            return (width, height);
        }
        return (null, null);
    }

    public static (int? x, int? y) GetSavedPosition()
    {
        if (Preferences.Default.ContainsKey(XKey) && Preferences.Default.ContainsKey(YKey))
        {
            int x = Preferences.Default.Get(XKey, 0);
            int y = Preferences.Default.Get(YKey, 0);
            return (x, y);
        }
        return (null, null);
    }

    public static bool? GetMaximizedState()
    {
        if (Preferences.Default.ContainsKey(IsMaximizedKey))
        {
            return Preferences.Default.Get(IsMaximizedKey, false);
        }
        return null;
    }
}
