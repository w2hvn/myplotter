using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PdfToGCode.Fonts
{
    public class GlyphRenderer
    {
        private readonly Dictionary<char, GlyphGeometry> _font;
        private readonly double _unitsPerEm;

        public GlyphRenderer(Dictionary<char, GlyphGeometry> font, double unitsPerEm = 1000)
        {
            _font = font;
            _unitsPerEm = unitsPerEm;
        }

        public Geometry RenderText(string text, double fontSize, Point origin)
        {
            var geometryGroup = new GeometryGroup();
            if (string.IsNullOrEmpty(text)) return geometryGroup;

            double currentX = origin.X;
            double currentY = origin.Y;
            double scale = fontSize / _unitsPerEm;

            foreach (char c in text)
            {
                if (_font.TryGetValue(c, out var glyph))
                {
                    // Clone geometry to apply transform without affecting original
                    var glyphGeom = glyph.GetPathGeometry().Clone();

                    var transformGroup = new TransformGroup();
                    // SVG font coordinates are Y-up (Cartesian). WPF is Y-down.
                    // So we flip Y.
                    transformGroup.Children.Add(new ScaleTransform(scale, -scale));

                    // Translate to position.
                    // Since we flipped Y, positive Y becomes negative.
                    // Baseline is 0. So 0 maps to 0.
                    // We want baseline at origin.Y.
                    transformGroup.Children.Add(new TranslateTransform(currentX, currentY));

                    glyphGeom.Transform = transformGroup;
                    geometryGroup.Children.Add(glyphGeom);

                    currentX += glyph.AdvanceWidth * scale;
                }
                else
                {
                    // Fallback for missing glyph (e.g. space if not in font)
                    // If space is not in font, advance by some default
                    if (char.IsWhiteSpace(c))
                    {
                        currentX += (_unitsPerEm / 4) * scale; // Approx space width
                    }
                }
            }
            return geometryGroup;
        }
    }
}
