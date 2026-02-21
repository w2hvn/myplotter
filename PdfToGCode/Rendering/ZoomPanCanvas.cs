using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfToGCode.Rendering
{
    public class ZoomPanCanvas : Canvas
    {
        private ScaleTransform _scaleTransform = new ScaleTransform();
        private TranslateTransform _translateTransform = new TranslateTransform();
        private Point _startPanPosition;
        private bool _isPanning;

        public ZoomPanCanvas()
        {
            var group = new TransformGroup();
            group.Children.Add(_scaleTransform);
            group.Children.Add(_translateTransform);
            this.RenderTransform = group;

            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
            this.MouseUp += OnMouseUp;
            this.MouseWheel += OnMouseWheel;
            this.ClipToBounds = true;
            this.Background = Brushes.Transparent; // Transparent background to capture mouse events
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Middle click or Ctrl+Left click to pan
            if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.Control))
            {
                // Capture mouse relative to parent to get screen/viewport delta
                _startPanPosition = e.GetPosition(this.Parent as UIElement);
                _isPanning = true;
                this.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var currentPosition = e.GetPosition(this.Parent as UIElement);
                var diff = currentPosition - _startPanPosition;
                _translateTransform.X += diff.X;
                _translateTransform.Y += diff.Y;
                _startPanPosition = currentPosition;
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                this.ReleaseMouseCapture();
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(this); // Local coordinates (untransformed document space)
            double scaleFactor = e.Delta > 0 ? 1.1 : 0.9;

            double oldScale = _scaleTransform.ScaleX;
            double newScale = oldScale * scaleFactor;

            // Limit zoom
            if (newScale < 0.1) newScale = 0.1;
            if (newScale > 50) newScale = 50;

            // Zoom towards mouse position:
            // ScreenPos = LocalPos * Scale + Translate
            // We want ScreenPos to remain constant for LocalPos.
            // (LocalPos * OldScale) + OldTranslate = (LocalPos * NewScale) + NewTranslate
            // NewTranslate = OldTranslate + LocalPos * (OldScale - NewScale)

            _translateTransform.X += pos.X * (oldScale - newScale);
            _translateTransform.Y += pos.Y * (oldScale - newScale);

            _scaleTransform.ScaleX = newScale;
            _scaleTransform.ScaleY = newScale;

            e.Handled = true;
        }

        public void Reset()
        {
            _scaleTransform.ScaleX = 1;
            _scaleTransform.ScaleY = 1;
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
        }
    }
}
