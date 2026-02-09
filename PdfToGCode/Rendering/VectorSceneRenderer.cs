using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PdfToGCode.Fonts;
using PdfToGCode.Pdf;
using PdfToGCode.Utils;

namespace PdfToGCode.Rendering
{
    public class VectorSceneRenderer
    {
        private readonly ZoomPanCanvas _canvas;
        private readonly GlyphRenderer _glyphRenderer;

        public VectorSceneRenderer(ZoomPanCanvas canvas, GlyphRenderer glyphRenderer)
        {
            _canvas = canvas;
            _glyphRenderer = glyphRenderer;
        }

        public void Render(List<PdfPageData> pages)
        {
            _canvas.Children.Clear();
            _canvas.Reset();

            foreach (var page in pages)
            {
                // Draw page border
                var border = new Rectangle
                {
                    Width = page.Width,
                    Height = page.Height,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(border, 0);
                Canvas.SetTop(border, 0);
                _canvas.Children.Add(border);

                foreach (var textItem in page.TextItems)
                {
                    // Convert origin from PDF (Bottom-Left) to Canvas (Top-Left)
                    var canvasOrigin = CoordinateMapper.PdfToCanvas(textItem.Origin, page.Height);

                    // Render text
                    // Note: GlyphRenderer handles Y-flip internally for the glyph geometry relative to the baseline origin.
                    var geometry = _glyphRenderer.RenderText(textItem.Text, textItem.FontSize, canvasOrigin);

                    var path = new Path
                    {
                        Data = geometry,
                        Stroke = Brushes.Blue,
                        StrokeThickness = 1 // Single line stroke
                    };

                    _canvas.Children.Add(path);
                }
            }
        }
    }
}
