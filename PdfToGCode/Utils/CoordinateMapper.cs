using System.Windows;

namespace PdfToGCode.Utils
{
    public static class CoordinateMapper
    {
        /// <summary>
        /// Converts PDF point (Bottom-Left origin) to Canvas point (Top-Left origin).
        /// </summary>
        public static Point PdfToCanvas(Point pdfPoint, double pageHeight, double scale = 1.0)
        {
            return new Point(pdfPoint.X * scale, (pageHeight - pdfPoint.Y) * scale);
        }

        /// <summary>
        /// Converts PDF Rect (Bottom-Left origin) to Canvas Rect (Top-Left origin).
        /// </summary>
        public static Rect PdfToCanvas(Rect pdfRect, double pageHeight, double scale = 1.0)
        {
            // PDF Rect stored as (Left, Bottom, Width, Height) in our extractor.
            // Canvas needs (Left, Top, Width, Height).
            // Top in Canvas corresponds to Top in PDF, but measured from Top.
            // PDF Top Y = Bottom + Height.
            // Canvas Top Y = PageHeight - PDF Top Y.

            double pdfTop = pdfRect.Y + pdfRect.Height;
            double canvasTop = pageHeight - pdfTop;

            return new Rect(pdfRect.X * scale, canvasTop * scale, pdfRect.Width * scale, pdfRect.Height * scale);
        }

        /// <summary>
        /// Converts Canvas point to G-Code point (Bottom-Left origin).
        /// Assuming G-Code space matches PDF space (Y-up).
        /// </summary>
        public static Point CanvasToGCode(Point canvasPoint, double pageHeight, double scale = 1.0)
        {
             double y = pageHeight - (canvasPoint.Y / scale);
             return new Point(canvasPoint.X / scale, y);
        }
    }
}
