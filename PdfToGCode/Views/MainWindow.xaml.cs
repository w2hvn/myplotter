using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        private void ToggleBusyState(bool isBusy)
        {
            LoadingBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;

            // Disable interactions
            BtnImport.IsEnabled = !isBusy;

            // Logic for other buttons depends on state, but we can just disable all during busy
            // and then re-evaluate enable state based on data presence
            if (isBusy)
            {
                BtnFont.IsEnabled = false;
                BtnGenerate.IsEnabled = false;
                BtnSave.IsEnabled = false;
            }
            else
            {
                // Restore state
                BtnFont.IsEnabled = _pagesData != null;
                BtnGenerate.IsEnabled = _glyphRenderer != null;
                BtnSave.IsEnabled = !string.IsNullOrEmpty(_gCode);
            }
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filepath = openFileDialog.FileName;

                try
                {
                    ToggleBusyState(true);

                    // Reset previous state
                    _glyphRenderer = null;
                    _gCode = null;
                    VectorCanvas.Children.Clear();
                    VectorCanvas.Reset(); // Reset zoom/pan

                    // Display in WebBrowser (must happen on UI thread)
                    PdfBrowser.Navigate(new Uri(filepath).AbsoluteUri);

                    // Extract Text (CPU bound, run in background)
                    _pagesData = await Task.Run(() =>
                    {
                        var loader = new PdfLoader();
                        using (var document = loader.Load(filepath))
                        {
                            var extractor = new PdfTextLayoutExtractor();
                            return extractor.ExtractText(document);
                        }
                    });

                    int itemCount = _pagesData?.Sum(p => p.TextItems.Count) ?? 0;
                    int pageCount = _pagesData?.Count ?? 0;

                    MessageBox.Show($"Imported {itemCount} text items from {pageCount} pages.", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing PDF: {ex.Message}", "Error");
                    _pagesData = null;
                }
                finally
                {
                    ToggleBusyState(false);
                }
            }
        }

        private async void BtnFont_Click(object sender, RoutedEventArgs e)
        {
            if (_pagesData == null) return;

            try
            {
                ToggleBusyState(true);

                // Prepare font path
                string fontPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "CHUINHOA.svg");
                if (!File.Exists(fontPath))
                {
                    string devPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Fonts", "CHUINHOA.svg");
                    if (File.Exists(devPath)) fontPath = devPath;
                    else
                    {
                         MessageBox.Show($"Font file not found at {fontPath}", "Error");
                         return;
                    }
                }

                // Parse Font (CPU bound)
                var font = await Task.Run(() =>
                {
                    var parser = new SvgFontParser();
                    return parser.Parse(fontPath);
                });

                _glyphRenderer = new GlyphRenderer(font);

                // Render Vector Scene (UI manipulation must be on UI thread, but preparation can be split if complex)
                // Since VectorSceneRenderer manipulates Canvas children directly, it must run on UI thread.
                // However, we can generate the geometries first if we refactor, but for now let's just run it.
                // If rendering is very slow, we might yield.

                var renderer = new VectorSceneRenderer(VectorCanvas, _glyphRenderer);

                // Small delay to allow UI to update to "Busy" state before freezing for rendering
                await Task.Delay(10);

                renderer.Render(_pagesData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rendering font: {ex.Message}", "Error");
            }
            finally
            {
                ToggleBusyState(false);
            }
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_pagesData == null || _glyphRenderer == null) return;

            try
            {
                ToggleBusyState(true);

                string fontPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "CHUINHOA.svg");
                if (!File.Exists(fontPath))
                {
                    string devPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Fonts", "CHUINHOA.svg");
                    if (File.Exists(devPath)) fontPath = devPath;
                }

                _gCode = await Task.Run(() =>
                {
                    var parser = new SvgFontParser();
                    var font = parser.Parse(fontPath);

                    var settings = new GCodeSettings(); // Use defaults
                    var generator = new GCodeGenerator(settings, font);
                    return generator.Generate(_pagesData);
                });

                MessageBox.Show($"Generated G-Code ({_gCode.Length} chars).", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating G-Code: {ex.Message}", "Error");
            }
            finally
            {
                ToggleBusyState(false);
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
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
                    ToggleBusyState(true);
                    string fileName = saveFileDialog.FileName;
                    await Task.Run(() => File.WriteAllText(fileName, _gCode));
                    MessageBox.Show("File saved successfully.", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error");
                }
                finally
                {
                    ToggleBusyState(false);
                }
            }
        }
    }
}
