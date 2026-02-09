using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using PdfToGCode.Fonts;
using PdfToGCode.GCode;
using PdfToGCode.Pdf;
using PdfToGCode.Rendering;

namespace PdfToGCode.Views
{
    public partial class MainWindow : Window
    {
        private List<PdfPageData>? _pagesData;
        private string? _gCode;
        private GlyphRenderer? _glyphRenderer;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filepath = openFileDialog.FileName;

                    // Display in WebBrowser
                    // Use Uri to avoid issues with paths
                    PdfBrowser.Navigate(new Uri(filepath).AbsoluteUri);

                    // Extract Text
                    var loader = new PdfLoader();
                    using (var document = loader.Load(filepath))
                    {
                        var extractor = new PdfTextLayoutExtractor();
                        _pagesData = extractor.ExtractText(document);
                    }

                    int itemCount = _pagesData?.Sum(p => p.TextItems.Count) ?? 0;
                    int pageCount = _pagesData?.Count ?? 0;

                    MessageBox.Show($"Imported {itemCount} text items from {pageCount} pages.", "Success");

                    BtnFont.IsEnabled = true;
                    BtnGenerate.IsEnabled = false;
                    BtnSave.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing PDF: {ex.Message}", "Error");
                }
            }
        }

        private void BtnFont_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_pagesData == null) return;

                // Load Font
                // Ensure fonts are copied to output directory
                string fontPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "CHUINHOA.svg");
                if (!File.Exists(fontPath))
                {
                    // Fallback search if running in dev environment without CopyToOutput
                    string devPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Fonts", "CHUINHOA.svg");
                    if (File.Exists(devPath)) fontPath = devPath;
                    else
                    {
                         MessageBox.Show($"Font file not found at {fontPath}", "Error");
                         return;
                    }
                }

                var parser = new SvgFontParser();
                var font = parser.Parse(fontPath);
                _glyphRenderer = new GlyphRenderer(font);

                // Render Vector Scene
                var renderer = new VectorSceneRenderer(VectorCanvas, _glyphRenderer);
                renderer.Render(_pagesData);

                BtnGenerate.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rendering font: {ex.Message}", "Error");
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_pagesData == null || _glyphRenderer == null) return;

                // Load font again for GCode generation to get raw paths
                // Ideally passing the dictionary would be cleaner but _glyphRenderer encapsulates it.
                // Re-parsing is fast enough.
                string fontPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "CHUINHOA.svg");
                if (!File.Exists(fontPath))
                {
                    // Use same fallback logic
                    string devPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Fonts", "CHUINHOA.svg");
                    if (File.Exists(devPath)) fontPath = devPath;
                }

                var parser = new SvgFontParser();
                var font = parser.Parse(fontPath);

                var settings = new GCodeSettings(); // Use defaults
                var generator = new GCodeGenerator(settings, font);
                _gCode = generator.Generate(_pagesData);

                MessageBox.Show($"Generated G-Code ({_gCode.Length} chars).", "Success");

                BtnSave.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating G-Code: {ex.Message}", "Error");
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_gCode)) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "G-Code Files (*.gcode;*.nc)|*.gcode;*.nc|All Files (*.*)|*.*",
                FileName = "output.gcode"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, _gCode);
                    MessageBox.Show("File saved successfully.", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error");
                }
            }
        }
    }
}
