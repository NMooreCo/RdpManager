using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace RdpManager.Controls
{
    /// <summary>
    /// Adorner that shows a horizontal insertion line during drag-drop operations
    /// </summary>
    public class InsertionAdorner : Adorner
    {
        private readonly bool _isTop;
        private readonly bool _isInto;
        private readonly System.Windows.Media.Pen _pen;
        private readonly System.Windows.Media.Brush _brush;

        public InsertionAdorner(UIElement adornedElement, bool isTop, bool isInto = false) : base(adornedElement)
        {
            _isTop = isTop;
            _isInto = isInto;

            // Create a blue pen for the insertion line (thicker for Into)
            _brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 118, 210)); // Material Design Blue
            _pen = new System.Windows.Media.Pen(_brush, _isInto ? 3 : 2);
            _pen.Freeze();

            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var adornedElement = AdornedElement as FrameworkElement;
            if (adornedElement == null) return;

            var renderSize = adornedElement.RenderSize;

            if (_isInto)
            {
                // Draw border around the entire item for "Into" drops
                var rect = new Rect(0, 0, renderSize.Width, renderSize.Height);
                drawingContext.DrawRectangle(null, _pen, rect);
            }
            else
            {
                // Draw horizontal line for Before/After drops
                var y = _isTop ? 0 : renderSize.Height;

                var startPoint = new System.Windows.Point(0, y);
                var endPoint = new System.Windows.Point(renderSize.Width, y);

                drawingContext.DrawLine(_pen, startPoint, endPoint);

                // Draw small circle at the start (left side)
                drawingContext.DrawEllipse(_brush, null, new System.Windows.Point(0, y), 3, 3);
            }
        }
    }
}
