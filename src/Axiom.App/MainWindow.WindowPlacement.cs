using Avalonia;
using Avalonia.Controls;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private void FitInitialWindowToScreen()
    {
        if (Screens.ScreenFromWindow(this) is not { } screen || WindowState != WindowState.Normal)
        {
            return;
        }

        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var availableWidth = Math.Max(1, area.Width - 16) / scale;
        var availableHeight = Math.Max(1, area.Height - 32) / scale;
        MinWidth  = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        Width     = Math.Min(Width, availableWidth);
        Height    = Math.Min(Height, availableHeight);
        var maximumX = Math.Max(area.X, area.Right - (int)Math.Ceiling(Width * scale) - 8);
        var maximumY = Math.Max(area.Y, area.Bottom - (int)Math.Ceiling(Height * scale) - 24);
        Position = new PixelPoint(Math.Clamp(Position.X
                                           , area.X
                                           , maximumX)
                                , Math.Clamp(Position.Y
                                           , area.Y
                                           , maximumY));
    }
}
