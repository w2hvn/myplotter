using System.Collections.Generic;
using System.Linq;
using System.Windows;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfToGCode.Pdf
{
    public class TextItem
    {
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Bounding box in PDF coordinates (Bottom-Left origin, Y-up).
        /// </summary>
        public Rect BoundingBox { get; set; }

        public double FontSize { get; set; }
        public int PageNumber { get; set; }

        /// <summary>
        /// Baseline origin in PDF coordinates (Bottom-Left origin, Y-up).
        /// </summary>
        public Point Origin { get; set; }
    }

    public class PdfPageData
    {
        public int PageNumber { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<TextItem> TextItems { get; set; } = new List<TextItem>();
        public List<List<Point>> GeometricPaths { get; set; } = new List<List<Point>>();
    }

    public class PdfTextLayoutExtractor
    {
        public List<PdfPageData> ExtractText(PdfDocument document)
        {
            var pagesData = new List<PdfPageData>();

            // GetPages() returns 1-based index pages usually.
            foreach (var page in document.GetPages())
            {
                var pageData = new PdfPageData
                {
                    PageNumber = page.Number,
                    Width = page.Width,
                    Height = page.Height
                };

                // Extract geometric paths (lines, rectangles for tables/borders)
                try
                {
                    // Use ExperimentalAccess.Paths as indicated by earlier code, although potentially obsolete,
                    // or try checking if there's a direct property.
                    // Based on warning: Use Page.Paths instead.
                    // But 'Page' interface in 0.1.13 might vary. Let's stick to ExperimentalAccess if it compiles (warning is fine)
                    // or try to switch to document.GetPages() returns Page.

                    // Note: accessing ExperimentalAccess.Paths
                    foreach (var path in page.ExperimentalAccess.Paths)
                    {
                        // Path is IEnumerable<PdfSubpath>
                        foreach (var subpath in path)
                        {
                            var currentPolyline = new List<Point>();
                            foreach (var command in subpath.Commands)
                            {
                                if (command is UglyToad.PdfPig.Core.PdfSubpath.Move move)
                                {
                                    if (currentPolyline.Count > 1)
                                    {
                                        pageData.GeometricPaths.Add(new List<Point>(currentPolyline));
                                    }
                                    currentPolyline.Clear();
                                    currentPolyline.Add(new Point(move.Location.X, move.Location.Y));
                                }
                                else if (command is UglyToad.PdfPig.Core.PdfSubpath.Line line)
                                {
                                    // PdfSubpath.Line uses 'To' property for destination
                                    currentPolyline.Add(new Point(line.To.X, line.To.Y));
                                }
                                else if (command is UglyToad.PdfPig.Core.PdfSubpath.Close)
                                {
                                    if (currentPolyline.Count > 0)
                                    {
                                        // Close the loop by adding the start point
                                        currentPolyline.Add(currentPolyline[0]);
                                        pageData.GeometricPaths.Add(new List<Point>(currentPolyline));
                                        currentPolyline.Clear();
                                    }
                                }
                            }

                            // Add any remaining open path
                            if (currentPolyline.Count > 1)
                            {
                                pageData.GeometricPaths.Add(new List<Point>(currentPolyline));
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error extracting geometry: {ex.Message}");
                }

                // Use GetWords() to group letters into words.
                foreach (var word in page.GetWords())
                {
                    var firstLetter = word.Letters.FirstOrDefault();
                    Point origin = new Point(word.BoundingBox.Left, word.BoundingBox.Bottom);
                    double fontSize = 12;

                    if (firstLetter != null)
                    {
                        // StartBaseLine is PdfPoint
                        origin = new Point(firstLetter.StartBaseLine.X, firstLetter.StartBaseLine.Y);
                        fontSize = firstLetter.PointSize; // PointSize is usually preferred over FontSize in PdfPig
                    }

                    pageData.TextItems.Add(new TextItem
                    {
                        Text = word.Text,
                        BoundingBox = new Rect(word.BoundingBox.Left, word.BoundingBox.Bottom, word.BoundingBox.Width, word.BoundingBox.Height),
                        FontSize = fontSize,
                        PageNumber = page.Number,
                        Origin = origin
                    });
                }
                pagesData.Add(pageData);
            }
            return pagesData;
        }
    }
}
