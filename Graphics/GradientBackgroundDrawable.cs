using Microsoft.Maui.Graphics;

namespace KopioRapido.Graphics;

public class GradientBackgroundDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Purple to Cyan gradient background matching the mockup
        var gradientPaint = new LinearGradientPaint
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops = new PaintGradientStop[]
            {
                new PaintGradientStop(0.0f, Color.FromRgb(139, 92, 246)),   // #8B5CF6 Purple
                new PaintGradientStop(0.5f, Color.FromRgb(167, 139, 250)),  // #A78BFA Light Purple
                new PaintGradientStop(1.0f, Color.FromRgb(103, 232, 249))   // #67E8F9 Cyan
            }
        };

        canvas.SetFillPaint(gradientPaint, dirtyRect);
        canvas.FillRectangle(dirtyRect);
    }
}
