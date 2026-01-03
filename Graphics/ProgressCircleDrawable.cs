using Microsoft.Maui.Graphics;

namespace KopioRapido.Graphics;

public class ProgressCircleDrawable : IDrawable
{
    public double Progress { get; set; } // 0 to 100
    public string StatusText { get; set; } = "READY";
    public bool IsActive { get; set; }

    private static bool _firstDrawLogged = false;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!_firstDrawLogged)
        {
            DiagnosticLogger.Log("=== ProgressCircleDrawable.Draw FIRST CALL ===");
            DiagnosticLogger.Log($"DirtyRect: {dirtyRect}, Progress: {Progress}, IsActive: {IsActive}");
            _firstDrawLogged = true;
        }

        try
        {
        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;
        var radius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2 - 20;

        // Draw outer glow (larger when active)
        if (IsActive)
        {
            var glowPaint = new RadialGradientPaint
            {
                Center = new Point(0.5, 0.5),
                Radius = 0.5,
                GradientStops = new PaintGradientStop[]
                {
                    new PaintGradientStop(0.0f, Color.FromRgba(251, 146, 60, 0.5f)),
                    new PaintGradientStop(0.5f, Color.FromRgba(249, 115, 22, 0.3f)),
                    new PaintGradientStop(1.0f, Color.FromRgba(234, 88, 12, 0.0f))
                }
            };
            canvas.SetFillPaint(glowPaint, dirtyRect);
            canvas.FillCircle(centerX, centerY, radius + 30);
        }

        // Draw background circle (dark)
        canvas.FillColor = Color.FromRgba(30, 41, 59, 0.8f);
        canvas.FillCircle(centerX, centerY, radius);

        // Draw progress arc
        if (Progress > 0)
        {
            canvas.StrokeColor = IsActive
                ? Color.FromRgb(249, 115, 22) // Orange when active
                : Color.FromRgb(103, 232, 249); // Cyan when idle
            canvas.StrokeSize = 8;

            var startAngle = -90; // Start at top
            var sweepAngle = (float)(360 * (Progress / 100.0));

            canvas.DrawArc(
                centerX - radius,
                centerY - radius,
                radius * 2,
                radius * 2,
                startAngle,
                sweepAngle,
                clockwise: true,
                closed: false
            );
        }

        // Draw outer ring
        canvas.StrokeColor = IsActive
            ? Color.FromRgba(251, 146, 60, 0.6f)
            : Color.FromRgba(103, 232, 249, 0.6f);
        canvas.StrokeSize = 2;
        canvas.DrawCircle(centerX, centerY, radius + 10);

        // Draw percentage text
        canvas.FontColor = Colors.White;
        canvas.FontSize = 48;
        var progressText = $"{Progress:F0}%";
        var textSize = canvas.GetStringSize(progressText, Microsoft.Maui.Graphics.Font.Default, 48);
        canvas.DrawString(
            progressText,
            centerX - textSize.Width / 2,
            centerY - 10,
            HorizontalAlignment.Left
        );

        // Draw status text
        canvas.FontSize = 14;
        canvas.FontColor = Color.FromRgba(255, 255, 255, 0.7f);
        var statusSize = canvas.GetStringSize(StatusText, Microsoft.Maui.Graphics.Font.Default, 14);
        canvas.DrawString(
            StatusText,
            centerX - statusSize.Width / 2,
            centerY + 20,
            HorizontalAlignment.Left
        );
        }
        catch (Exception ex)
        {
            DiagnosticLogger.LogException("ProgressCircleDrawable.Draw", ex);
            // Don't rethrow - allow the app to continue even if drawing fails
        }
    }
}
