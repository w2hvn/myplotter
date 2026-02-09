using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace PdfToGCode.Fonts
{
    public class SvgFontParser
    {
        public Dictionary<char, GlyphGeometry> Parse(string filepath)
        {
            var glyphs = new Dictionary<char, GlyphGeometry>();
            if (!File.Exists(filepath)) return glyphs;

            try
            {
                var doc = XDocument.Load(filepath);
                var font = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "font");
                if (font == null) return glyphs;

                var glyphElements = font.Descendants().Where(e => e.Name.LocalName == "glyph");

                foreach (var glyph in glyphElements)
                {
                    var unicodeAttr = glyph.Attribute("unicode");
                    if (unicodeAttr == null) continue;

                    string unicodeStr = unicodeAttr.Value;
                    if (string.IsNullOrEmpty(unicodeStr)) continue;

                    // Handle single char
                    char unicode = unicodeStr[0];

                    double advance = 0;
                    var advAttr = glyph.Attribute("horiz-adv-x");
                    if (advAttr != null) double.TryParse(advAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out advance);
                    else
                    {
                        // Fallback to font default
                        var fontAttr = font.Attribute("horiz-adv-x");
                        if (fontAttr != null) double.TryParse(fontAttr.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out advance);
                    }

                    var dAttr = glyph.Attribute("d");
                    string d = dAttr?.Value ?? "";

                    var glyphGeom = new GlyphGeometry
                    {
                        Unicode = unicode,
                        AdvanceWidth = advance,
                        Paths = ParsePathData(d)
                    };
                    glyphs[unicode] = glyphGeom;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing SVG font: {ex.Message}");
            }
            return glyphs;
        }

        private List<List<Point>> ParsePathData(string d)
        {
            var paths = new List<List<Point>>();
            if (string.IsNullOrWhiteSpace(d)) return paths;

            // Regex to find commands (single letters) or numbers
            // Command letters: M, L (we only expect M and L per spec)
            // Numbers: simple float format
            string pattern = @"([MLml])|([\-+]?[0-9]*\.?[0-9]+)";
            var matches = Regex.Matches(d, pattern);

            var tokens = new List<string>();
            foreach (Match m in matches)
            {
                if (!string.IsNullOrWhiteSpace(m.Value))
                    tokens.Add(m.Value);
            }

            List<Point>? currentPath = null;
            string currentCommand = "";

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (char.IsLetter(token[0]))
                {
                    currentCommand = token.ToUpper();
                    // SVG font spec says: "Path data contains only M and L commands".
                    // Relative commands (lowercase) are possible but snippet uses uppercase.
                    // Assuming absolute coordinates for now.
                    continue;
                }

                // If token is number, it's a coordinate
                if (currentCommand == "M")
                {
                    if (i + 1 >= tokens.Count) break;
                    if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double x) &&
                        double.TryParse(tokens[i + 1], NumberStyles.Any, CultureInfo.InvariantCulture, out double y))
                    {
                        currentPath = new List<Point> { new Point(x, y) };
                        paths.Add(currentPath);
                        i++; // Consume y
                        // Implicit lineto after moveto?
                        // "If a moveto is followed by multiple pairs of coordinates, the subsequent pairs are treated as implicit lineto commands."
                        currentCommand = "L";
                    }
                }
                else if (currentCommand == "L")
                {
                    if (currentPath == null) continue;
                    if (i + 1 >= tokens.Count) break;
                    if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double x) &&
                        double.TryParse(tokens[i + 1], NumberStyles.Any, CultureInfo.InvariantCulture, out double y))
                    {
                        currentPath.Add(new Point(x, y));
                        i++; // Consume y
                    }
                }
            }
            return paths;
        }
    }
}
