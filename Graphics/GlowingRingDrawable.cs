using Microsoft.Maui.Graphics;

namespace KopioRapido.Graphics;

public class GlowingRingDrawable : IDrawable
{
    public bool IsActive { get; set; }
    public string IconType { get; set; } = "folder"; // "folder" or "cloud"

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;
        var radius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2 - 10;

        // Colors based on active state
        var glowColor = IsActive
            ? Color.FromRgb(251, 146, 60)  // Orange when active
            : Color.FromRgb(103, 232, 249); // Cyan when idle

        // Draw outer glow (radial gradient)
        var glowPaint = new RadialGradientPaint
        {
            Center = new Point(0.5, 0.5),
            Radius = 0.5,
            GradientStops = new PaintGradientStop[]
            {
                new PaintGradientStop(0.0f, Color.FromRgba(glowColor.Red, glowColor.Green, glowColor.Blue, 0.3f)),
                new PaintGradientStop(0.5f, Color.FromRgba(glowColor.Red, glowColor.Green, glowColor.Blue, 0.2f)),
                new PaintGradientStop(1.0f, Color.FromRgba(glowColor.Red, glowColor.Green, glowColor.Blue, 0.0f))
            }
        };
        canvas.SetFillPaint(glowPaint, dirtyRect);
        canvas.FillCircle(centerX, centerY, radius + 20);

        // Draw main ring
        canvas.StrokeColor = glowColor;
        canvas.StrokeSize = 2;
        canvas.DrawCircle(centerX, centerY, radius);

        // Draw inner circle (dark background)
        canvas.FillColor = Color.FromRgba(30, 41, 59, 0.8f);
        canvas.FillCircle(centerX, centerY, radius - 5);

        // Draw icon in center
        if (IconType == "folder")
        {
            DrawFolderIcon(canvas, centerX, centerY);
        }
        else if (IconType == "cloud")
        {
            DrawCloudIcon(canvas, centerX, centerY);
        }
    }

    private void DrawFolderIcon(ICanvas canvas, float centerX, float centerY)
    {
        // Purple gradient folder icon
        var iconSize = 50f;
        var left = centerX - iconSize / 2;
        var top = centerY - iconSize / 2;

        // Folder tab
        canvas.FillColor = Color.FromRgb(167, 139, 250); // #A78BFA
        canvas.FillRectangle(left, top, iconSize * 0.6f, iconSize * 0.25f);

        // Folder body
        canvas.FillColor = Color.FromRgb(139, 92, 246); // #8B5CF6
        canvas.FillRectangle(left, top + iconSize * 0.25f, iconSize, iconSize * 0.65f);

        // Folder line detail
        canvas.StrokeColor = Color.FromRgb(196, 181, 253); // Lighter purple
        canvas.StrokeSize = 1;
        canvas.DrawLine(left, top + iconSize * 0.35f, left + iconSize, top + iconSize * 0.35f);
    }

    private void DrawCloudIcon(ICanvas canvas, float centerX, float centerY)
    {
        // Green gradient cloud icon
        var iconSize = 50f;
        var left = centerX - iconSize / 2;
        var top = centerY - iconSize / 2 + 5;

        canvas.FillColor = Color.FromRgb(110, 231, 183); // #6EE7B7

        // Cloud shape using circles
        // Left bump
        canvas.FillCircle(left + iconSize * 0.25f, top + iconSize * 0.4f, iconSize * 0.25f);
        // Top bump
        canvas.FillCircle(left + iconSize * 0.5f, top + iconSize * 0.25f, iconSize * 0.3f);
        // Right bump
        canvas.FillCircle(left + iconSize * 0.75f, top + iconSize * 0.4f, iconSize * 0.25f);
        // Bottom base
        canvas.FillRectangle(left + iconSize * 0.15f, top + iconSize * 0.4f, iconSize * 0.7f, iconSize * 0.3f);

        // Storage indicator bar at bottom
        canvas.FillColor = Color.FromRgb(16, 185, 129); // #10B981
        canvas.FillRoundedRectangle(left + iconSize * 0.25f, top + iconSize * 0.8f, iconSize * 0.5f, iconSize * 0.1f, 5);
    }
}
