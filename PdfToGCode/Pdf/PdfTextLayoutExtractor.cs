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
