using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using DrawClient.ViewModels;

namespace DrawClient.Views.UserControls
{
    public partial class Canvas : UserControl
    {
        private Point lastPoint;
        private bool isDrawing = false;
        private CanvasViewModel _viewModel;

        public Canvas()
        {
            InitializeComponent();
            this.DataContextChanged += Canvas_DataContextChanged;
        }

        private void Canvas_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is CanvasViewModel oldVm)
            {
                oldVm.OnLineReceived -= DrawNetworkLine;
            }

            if (e.NewValue is CanvasViewModel newVm)
            {
                _viewModel = newVm;
                _viewModel.OnLineReceived += DrawNetworkLine;
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isDrawing = true;
            lastPoint = e.GetPosition(MyCanvas);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawing || e.LeftButton != MouseButtonState.Pressed || _viewModel == null)
                return;

            Point currentPoint = e.GetPosition(MyCanvas);

            // Gọi hàm DrawLineLocal để vẽ nội bộ vì InkCanvas đã bị đặt thành EditingMode="None"
            DrawLineLocal(lastPoint, currentPoint, _viewModel.CurrentColor, _viewModel.CurrentThickness);

            // Gửi dữ liệu qua mạng cho người khác
            _viewModel.SendDrawData(lastPoint, currentPoint);

            lastPoint = currentPoint;
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDrawing = false;

            // Thả chuột ra để các thành phần khác của app hoạt động lại bình thường
            if (MyCanvas.IsMouseCaptured)
            {
                MyCanvas.ReleaseMouseCapture();
            }
        }

        private void DrawLineLocal(Point p1, Point p2, string hexColor, double thickness)
        {
            if (string.IsNullOrEmpty(hexColor)) hexColor = "#000000";

            StylusPointCollection points = new StylusPointCollection
            {
                new StylusPoint(p1.X, p1.Y),
                new StylusPoint(p2.X, p2.Y)
            };

            Stroke stroke = new Stroke(points)
            {
                DrawingAttributes = new DrawingAttributes
                {
                    Color = (Color)ColorConverter.ConvertFromString(hexColor),
                    Width = thickness,
                    Height = thickness
                }
            };

            MyCanvas.Strokes.Add(stroke);
        }

        private void DrawNetworkLine(Point p1, Point p2, string hexColor, double thickness)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DrawLineLocal(p1, p2, hexColor, thickness);
            });
        }
    }
}