using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PdfToGCode.Fonts
{
    public class GlyphGeometry
    {
        public char Unicode { get; set; }
        public double AdvanceWidth { get; set; }
        public List<List<Point>> Paths { get; set; } = new List<List<Point>>();

        public Geometry GetPathGeometry()
        {
            var geometry = new PathGeometry();
            foreach (var polyline in Paths)
            {
                if (polyline.Count == 0) continue;

                var figure = new PathFigure { StartPoint = polyline[0], IsClosed = false };
                if (polyline.Count > 1)
                {
                    var segments = new PolyLineSegment(polyline.Skip(1), true);
                    figure.Segments.Add(segments);
                }
                geometry.Figures.Add(figure);
            }
            return geometry;
        }
    }
}
