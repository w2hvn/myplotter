using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using PdfToGCode.Fonts;
using PdfToGCode.Pdf;

namespace PdfToGCode.GCode
{
    public class GCodeGenerator
    {
        private readonly GCodeSettings _settings;
        private readonly Dictionary<char, GlyphGeometry> _font;
        private readonly double _unitsPerEm = 1000;

        public GCodeGenerator(GCodeSettings settings, Dictionary<char, GlyphGeometry> font)
        {
            _settings = settings;
            _font = font;
        }

        public string Generate(List<PdfPageData> pages)
        {
            var sb = new StringBuilder();
            sb.AppendLine("%");
            sb.AppendLine("G21"); // mm units
            sb.AppendLine("G90"); // Absolute positioning
            sb.AppendLine($"F{_settings.FeedRate}");

            foreach (var page in pages)
            {
                // 1. Generate Geometry (Tables, Lines) first
                foreach (var polyline in page.GeometricPaths)
                {
                     if (polyline.Count < 2) continue;

                     var transformedPath = new List<Point>();
                     foreach (var p in polyline)
                     {
                         // PDF points are already absolute in PDF space (Bottom-Left origin)
                         // Just scale to machine units
                         transformedPath.Add(new Point(p.X * _settings.Scale, p.Y * _settings.Scale));
                     }
                     AppendPath(sb, transformedPath);
                }

                // 2. Sort text items: Top to Bottom (Descending Y), Left to Right (Ascending X)
                // Note: PDF Y coordinates increase upwards, so higher Y is "Top".
                var sortedItems = page.TextItems
                    .OrderByDescending(t => t.Origin.Y)
                    .ThenBy(t => t.Origin.X)
                    .ToList();

                foreach (var item in sortedItems)
                {
                    double currentX = item.Origin.X;
                    double currentY = item.Origin.Y;
                    double scale = (item.FontSize / _unitsPerEm);

                    foreach (char c in item.Text)
                    {
                        if (_font.TryGetValue(c, out var glyph))
                        {
                            foreach (var polyline in glyph.Paths)
                            {
                                if (polyline.Count == 0) continue;

                                var transformedPath = new List<Point>();
                                foreach (var p in polyline)
                                {
                                    // Scale glyph relative to baseline origin
                                    // Note: Glyph coordinates are relative to (0,0) baseline.
                                    // Y is up in both PDF and Font coords.
                                    double px = p.X * scale;
                                    double py = p.Y * scale;

                                    // Translate to position (in PDF points)
                                    px += currentX;
                                    py += currentY;

                                    // Convert to machine units (mm)
                                    px *= _settings.Scale;
                                    py *= _settings.Scale;

                                    transformedPath.Add(new Point(px, py));
                                }

                                AppendPath(sb, transformedPath);
                            }
                            currentX += glyph.AdvanceWidth * scale;
                        }
                        else
                        {
                             // Fallback spacing for whitespace or missing chars
                             if (char.IsWhiteSpace(c))
                             {
                                 currentX += (_unitsPerEm / 4) * scale;
                             }
                        }
                    }
                }
            }

            sb.AppendLine("M30");
            return sb.ToString();
        }

        private void AppendPath(StringBuilder sb, List<Point> path)
        {
            if (path.Count == 0) return;

            // Move to start point (Rapid)
            var start = path[0];
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "G0 Z{0:F3}", _settings.ZUp));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "G0 X{0:F3} Y{1:F3}", start.X, start.Y));

            // Plunge (Rapid to cut height)
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "G0 Z{0:F3}", _settings.ZDown));

            // Cut path (Feed)
            for (int i = 1; i < path.Count; i++)
            {
                var p = path[i];
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "G1 X{0:F3} Y{1:F3}", p.X, p.Y));
            }

            // Lift (Rapid)
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "G0 Z{0:F3}", _settings.ZUp));
        }
    }
}
