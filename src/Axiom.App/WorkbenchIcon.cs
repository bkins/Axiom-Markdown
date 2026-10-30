using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Media;

namespace Axiom.App;

internal sealed class WorkbenchIcon : Viewbox
{
    internal WorkbenchIcon(string geometry)
    {
        Width = Height = 17;
        var path = new Avalonia.Controls.Shapes.Path
                   {
                       Data            = Geometry.Parse(geometry)
                     , StrokeThickness = 1.6
                     , StrokeLineCap   = PenLineCap.Round
                     , StrokeJoin      = PenLineJoin.Round
                     , Width           = 24
                     , Height          = 24
                   };
        path.Bind(Shape.StrokeProperty
                , new Binding("Foreground")
                  {
                      RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Button) }
                  });
        Child = path;
    }
}
