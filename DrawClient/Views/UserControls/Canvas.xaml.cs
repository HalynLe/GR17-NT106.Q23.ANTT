using DrawClient.ViewModels;
using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

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

            this.Loaded += (s, e) =>
            {
                _viewModel = this.DataContext as CanvasViewModel;
                if (_viewModel != null)
                {
                    _viewModel.OnLineReceived += DrawNetworkLine;
                }
            };

            this.Unloaded += (s, e) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.OnLineReceived -= DrawNetworkLine;
                }
            };
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isDrawing = true;
            lastPoint = e.GetPosition(MyCanvas);
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDrawing = false;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawing || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point currentPoint = e.GetPosition(MyCanvas);

            // vẽ local
            DrawLineLocal(lastPoint, currentPoint, _viewModel.CurrentColor, _viewModel.CurrentThickness);

            _viewModel.SendDrawData(lastPoint, currentPoint);

            lastPoint = currentPoint;
        }

        private void DrawLineLocal(Point p1, Point p2, string hexColor, double thickness)
        {
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