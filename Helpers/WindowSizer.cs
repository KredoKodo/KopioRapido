namespace KopioRapido.Helpers;

public static class WindowSizer
{
    private const double GoldenRatio = 1.618;
    private const int MinWidth = 750;
    private const int MinHeight = 550;
    private const int MaxWidth = 1600;
    private const int MaxHeight = 1200;
    private const double WorkAreaPercentage = 0.70; // Conservative 70% of work area

    public static (int width, int height) CalculateDefaultSize(int screenWidth, int screenHeight)
    {
        // Calculate 70% of work area
        int targetWidth = (int)(screenWidth * WorkAreaPercentage);
        int targetHeight = (int)(screenHeight * WorkAreaPercentage);

        // Apply golden ratio: prioritize width, derive height
        // Width:Height = 1.618:1
        int goldenHeight = (int)(targetWidth / GoldenRatio);

        // If golden ratio height fits in target, use it
        if (goldenHeight <= targetHeight)
        {
            targetHeight = goldenHeight;
        }
        else
        {
            // Screen is too narrow, prioritize height and derive width
            targetWidth = (int)(targetHeight * GoldenRatio);
        }

        // Clamp to minimum constraints
        if (targetWidth < MinWidth)
            targetWidth = MinWidth;
        if (targetHeight < MinHeight)
            targetHeight = MinHeight;

        // Clamp to maximum constraints
        if (targetWidth > MaxWidth)
            targetWidth = MaxWidth;
        if (targetHeight > MaxHeight)
            targetHeight = MaxHeight;

        // Ensure golden ratio is still approximately maintained after clamping
        // If we hit max constraints, re-adjust to maintain ratio
        if (targetWidth == MaxWidth)
        {
            int adjustedHeight = (int)(targetWidth / GoldenRatio);
            if (adjustedHeight >= MinHeight && adjustedHeight <= MaxHeight)
                targetHeight = adjustedHeight;
        }
        else if (targetHeight == MaxHeight)
        {
            int adjustedWidth = (int)(targetHeight * GoldenRatio);
            if (adjustedWidth >= MinWidth && adjustedWidth <= MaxWidth)
                targetWidth = adjustedWidth;
        }

        return (targetWidth, targetHeight);
    }

    public static (int width, int height) ValidateSavedSize(int savedWidth, int savedHeight, int screenWidth, int screenHeight)
    {
        // Check if saved size is valid
        bool isValid = savedWidth >= MinWidth &&
                       savedHeight >= MinHeight &&
                       savedWidth <= screenWidth &&
                       savedHeight <= screenHeight;

        if (!isValid)
        {
            // Saved size is invalid, calculate default
            return CalculateDefaultSize(screenWidth, screenHeight);
        }

        return (savedWidth, savedHeight);
    }

    public static (int x, int y) ValidateSavedPosition(int savedX, int savedY, int windowWidth, int windowHeight, int screenWidth, int screenHeight)
    {
        // Check if window would be visible on screen
        bool isVisible = savedX >= 0 &&
                         savedY >= 0 &&
                         savedX + windowWidth <= screenWidth &&
                         savedY + windowHeight <= screenHeight;

        if (!isVisible)
        {
            // Center the window
            return CenterWindow(windowWidth, windowHeight, screenWidth, screenHeight);
        }

        return (savedX, savedY);
    }

    public static (int x, int y) CenterWindow(int windowWidth, int windowHeight, int screenWidth, int screenHeight)
    {
        int x = (screenWidth - windowWidth) / 2;
        int y = (screenHeight - windowHeight) / 2;

        // Ensure not negative
        if (x < 0) x = 0;
        if (y < 0) y = 0;

        return (x, y);
    }
}
