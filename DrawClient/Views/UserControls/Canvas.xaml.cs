using DrawClient.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using DrawClient.Models;
using System.Linq;
using System.IO;
using System.Windows.Media.Imaging;
using DrawClient.Services;

namespace DrawClient.Views.UserControls
{
    public partial class Canvas : UserControl
    {
        private Point lastPoint;
        private bool isDrawing = false;
        private CanvasViewModel _viewModel;
        private Point _startPoint;
        private Stroke _currentTempStroke; // Stroke tạm thời để hiển thị khi đang kéo chuột
        private bool isShapeDrawing = false;
        private System.Windows.Shapes.Rectangle _ocrSelectionRect;
        private Point _ocrStartPoint;

        public Canvas()
        {
            InitializeComponent();

            this.DataContextChanged += Canvas_DataContextChanged;

            this.PreviewMouseDown += UserControl_PreviewMouseDown;

            // FIX MEMORY LEAK
            this.Unloaded += Canvas_Unloaded;

            // Khởi tạo thuộc tính vẽ mặc định cho Canvas
            MyCanvas.DefaultDrawingAttributes = new DrawingAttributes
            {
                FitToCurve = true,
                IgnorePressure = true,
                Width = 2,
                Height = 2,
                Color = Colors.Black
            };
        }

        private void Canvas_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.OnLineReceived -= DrawNetworkLine;
                _viewModel.OnCanvasCleared -= ClearLocalCanvas;
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.OnShapeReceived -= DrawShape;
                _viewModel.OnTextReceived -= DrawText;
                // FIX SOCKET MEMORY LEAK
                _viewModel.Cleanup();
            }
        }

        private void ChatMessages_CollectionChanged(
            object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ChatScrollViewer?.ScrollToEnd();
                    }));
                }

        private void Canvas_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 1. Unsubscribe VM cũ trước
            if (e.OldValue is CanvasViewModel oldVm)
            {
                oldVm.OnLineReceived -= DrawNetworkLine;
                oldVm.OnCanvasCleared -= ClearLocalCanvas;
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
                oldVm.OnShapeReceived -= DrawShape;
                oldVm.OnTextReceived -= DrawText;

                if (oldVm.Toolbar != null)
                {
                    oldVm.Toolbar.PropertyChanged -= Toolbar_PropertyChanged;
                    oldVm.Toolbar.ToolSelected -= Toolbar_ToolSelected;
                }
            }

            // 2. Gán VM mới
            if (e.NewValue is CanvasViewModel newVm)
            {
                _viewModel = newVm;

                // 3. Subscribe đúng instance
                _viewModel.OnLineReceived += DrawNetworkLine;
                _viewModel.OnCanvasCleared += ClearLocalCanvas;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                _viewModel.OnShapeReceived += DrawShape;
                _viewModel.OnTextReceived += DrawText;
                _viewModel.ChatMessages.CollectionChanged += ChatMessages_CollectionChanged;

                if (_viewModel.Toolbar != null)
                {
                    _viewModel.Toolbar.PropertyChanged += Toolbar_PropertyChanged;
                    _viewModel.Toolbar.ToolSelected += Toolbar_ToolSelected;
                }

                UpdateCurrentDrawingAttributes(_viewModel);
                 _viewModel.OnUndoRedo += RedrawAllFromActions;
            }
        }

                    private void RedrawAllFromActions()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MyCanvas.Strokes.Clear();
                if (_viewModel == null) return;
                var actions = _viewModel.UndoRedoManager.GetAllActions().ToList();
                foreach (var action in actions)
                {
                    DrawSingleAction(action);
                }
            });
        }

        private void DrawSingleAction(DrawAction action)
        {
            switch (action.ActionType)
            {
                case "DRAW":
                    DrawLineLocal(action.StartPoint, action.EndPoint, action.Color, action.Thickness);
                    break;
                case "SHAPE":
                    var points = CreateShapePointsFromAction(action);
                    if (points != null)
                    {
                        var stroke = new Stroke(points)
                        {
                            DrawingAttributes = new DrawingAttributes
                            {
                                Color = (Color)ColorConverter.ConvertFromString(action.Color),
                                Width = action.Thickness,
                                Height = action.Thickness,
                                FitToCurve = false,
                                IgnorePressure = true
                            }
                        };
                        MyCanvas.Strokes.Add(stroke);
                    }
                    break;
                // ERASE không cần xử lý vì nét đó đã bị xóa khỏi danh sách action
            }
        }

        private StylusPointCollection CreateShapePointsFromAction(DrawAction action)
        {
            Point start = action.StartPoint;
            Point end = action.EndPoint;
            string shapeType = action.ShapeType?.ToLower();
            switch (shapeType)
            {
                case "rectangle":
                case "square":
                    return CreateRectanglePoints(start, end);
                case "circle":
                case "ellipse":
                    return CreateEllipsePoints(start, end);
                case "triangle":
                    return CreateTrianglePoints(start, end);
                case "line":
                    return CreateLinePoints(start, end);
                default:
                    return null;
            }
        }

        // Lắng nghe mỗi khi Size, Màu hoặc Loại bút thay đổi trong ViewModel
        private void Toolbar_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_viewModel != null && (e.PropertyName == "CurrentThickness" ||
                                       e.PropertyName == "PencilSize" ||
                                       e.PropertyName == "EraserSize" ||
                                       e.PropertyName == "CurrentColor" ||
                                       e.PropertyName == "CurrentPenType" ||
                                       e.PropertyName == "IsEraserSelected" ||
                                       e.PropertyName == "IsPencilSelected"))
            {
                UpdateCurrentDrawingAttributes(_viewModel);
            }
        }

        private void Toolbar_ToolSelected(object sender, string e)
        {
            if (_viewModel != null)
            {
                UpdateCurrentDrawingAttributes(_viewModel);
            }
        }

        // Cập nhật trực tiếp lên Canvas thực tế
        private void UpdateCurrentDrawingAttributes(CanvasViewModel vm)
        {
            if (vm?.Toolbar == null) return;

            string penType = vm.Toolbar.CurrentPenType?.ToLowerInvariant();
            string selectedTool = vm.SelectedTool?.ToLowerInvariant();
            double size = vm.Toolbar.IsEraserSelected ? vm.Toolbar.EraserSize : vm.Toolbar.PencilSize;
            bool isEraser = vm.Toolbar.IsEraserSelected || selectedTool == "eraser";
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(_viewModel.Toolbar.CurrentColor);
                var attributes = new DrawingAttributes
                {
                    Color = color,
                    Width = size,
                    Height = size,
                    FitToCurve = true,
                    IgnorePressure = true,
                    IsHighlighter = false,
                    StylusTip = StylusTip.Ellipse
                };

                // Chỉnh nét vẽ cho từng loại bút
                switch (vm.Toolbar.CurrentPenType)
                {
                    case "Fountain":
                        attributes.StylusTip = StylusTip.Rectangle;
                        attributes.Width = size * 0.4;
                        attributes.Height = size * 1.5;
                        break;
                    case "Highlighter":
                        attributes.IsHighlighter = true;
                        attributes.Height = size * 1.5;
                        attributes.Width = size * 1.5;
                        attributes.StylusTip = StylusTip.Rectangle;
                        attributes.Color = System.Windows.Media.Color.FromArgb(120, color.R, color.G, color.B);
                        break;
                    case "Laser":
                        attributes.Color = System.Windows.Media.Color.FromArgb(150, 255, 0, 0);
                        attributes.Width = size;
                        break;
                    case "Brush":
                        attributes.StylusTip = StylusTip.Ellipse;
                        attributes.Width = size;
                        attributes.Height = size;
                        attributes.IsHighlighter = false;
                        break;
                    default:
                        attributes.Width = size;
                        attributes.Height = size;
                        break;
                }

                MyCanvas.DefaultDrawingAttributes = attributes;
                if (EraserCursor != null)
                {
                    EraserCursor.Width = size;
                    EraserCursor.Height = size;
                }

                var shapes = new List<string> { "square", "circle", "triangle", "line", "rectangle", "ellipse" };

                if (isEraser)
                {
                    MyCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                }
                else if (_viewModel.SelectedTool?.ToLower() == "shape")
                {
                    MyCanvas.EditingMode = InkCanvasEditingMode.None;
                    return;
                }
                else if (_viewModel.SelectedTool?.ToLower() == "select")
                {
                    MyCanvas.EditingMode = InkCanvasEditingMode.Select;
                }
                else
                {
                    MyCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi Update nét vẽ: " + ex.Message);
            }
        }
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_viewModel == null) return;

            // TOOL / MODE
            if (e.PropertyName == nameof(CanvasViewModel.CurrentEditingMode) ||
                e.PropertyName == nameof(CanvasViewModel.SelectedTool))
            {
                bool isEraser = _viewModel.SelectedTool?.ToLower() == "eraser";

                if (isEraser)
                {
                    MyCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                }
                else if (_viewModel.SelectedTool?.ToLower() == "shape")
                {
                    MyCanvas.EditingMode = InkCanvasEditingMode.None;
                    return;
                }
                else
                {
                    MyCanvas.EditingMode = _viewModel.CurrentEditingMode;

                    if (_viewModel.CurrentEditingMode == InkCanvasEditingMode.Select)
                    {
                        MyCanvas.UseCustomCursor = false;
                    }
                }
            }
        }

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel == null)
                return;
            lastPoint = e.GetPosition(MyCanvas); // Cập nhật điểm bắt đầu ngay khi nhấn chuột
            _startPoint = lastPoint;

            DependencyObject source = e.OriginalSource as DependencyObject;

            while (source != null)
            {
                if (source is FrameworkElement fe)
                {
                    if (fe.Name == "ProfilePopover")
                        return;

                    if (fe is Button button &&
                        button.Command == _viewModel.ToggleProfilePopoverCommand)
                    {
                        return;
                    }
                }

                source = VisualTreeHelper.GetParent(source);
            }

            _viewModel.IsProfilePopoverVisible = false;
        }

        private void ClearLocalCanvas()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MyCanvas.Strokes.Clear();
            });
        }

        private void ProfilePopover_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel?.Toolbar == null) return;

            // ocr
            if (_viewModel.SelectedTool?.ToLowerInvariant() == "ocr")
            {
                MyCanvas.EditingMode = InkCanvasEditingMode.None;

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    _ocrStartPoint = e.GetPosition(MyCanvas);

                    _ocrSelectionRect = new System.Windows.Shapes.Rectangle
                    {
                        Stroke = Brushes.DeepSkyBlue,
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection { 4, 2 },
                        Fill = new SolidColorBrush(Color.FromArgb(25, 0, 120, 215))
                    };

                    System.Windows.Controls.Canvas.SetLeft(_ocrSelectionRect, _ocrStartPoint.X);
                    System.Windows.Controls.Canvas.SetTop(_ocrSelectionRect, _ocrStartPoint.Y);
                    OverlayCanvas.Children.Add(_ocrSelectionRect);

                    MyCanvas.CaptureMouse();
                    MyCanvas.Cursor = Cursors.Cross;
                }
                return;
            }

            // KHỞI TẠO CÁC BIẾN KIỂM TRA TRẠNG THÁI
            string penType = _viewModel.Toolbar.CurrentPenType?.ToLowerInvariant();
            string selectedTool = _viewModel.SelectedTool?.ToLowerInvariant();
            bool isEraser = _viewModel.Toolbar.IsEraserSelected || selectedTool == "eraser";

            var shapes = new List<string> { "square", "circle", "triangle", "line", "rectangle", "ellipse" };
            bool isShape = penType != null && shapes.Contains(penType) && selectedTool == "shape";

            // Thoát nếu không thuộc chế độ được phép vẽ
            if (_viewModel.CurrentEditingMode != InkCanvasEditingMode.Ink
                && !isEraser
                && !isShape)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            // XỬ LÝ KHI DÙNG CỤC TẨY
            if (isEraser)
            {
                isDrawing = true;
                lastPoint = e.GetPosition(MyCanvas);
                MyCanvas.CaptureMouse();
                UpdateEraserCursor(lastPoint);
                if (EraserCursor != null) EraserCursor.Visibility = Visibility.Visible;
                return;
            }

            // XỬ LÝ KHI VẼ HÌNH KHỐI
            if (isShape)
            {
                isShapeDrawing = true; // Chỉ kích hoạt vẽ hình khi click hẳn vào Canvas
                _startPoint = e.GetPosition(MyCanvas);
                MyCanvas.CaptureMouse();
                MyCanvas.EditingMode = InkCanvasEditingMode.None;
                return;
            }

            // XỬ LÝ KHI VẼ BÚT BÌNH THƯỜNG
            if (_viewModel.CurrentEditingMode == InkCanvasEditingMode.Ink)
            {
                isDrawing = true;
                lastPoint = e.GetPosition(MyCanvas);
                MyCanvas.CaptureMouse();
            }
        }
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_viewModel?.Toolbar == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point currentPoint = e.GetPosition(MyCanvas);

            // Tránh xử lý nếu chuột không thực sự di chuyển (tiết kiệm tài nguyên)
            if (currentPoint == lastPoint) return;

            string tool = _viewModel.SelectedTool?.ToLowerInvariant();
            string penType = _viewModel.Toolbar.CurrentPenType?.ToLowerInvariant();
            bool isEraser = _viewModel.Toolbar.IsEraserSelected || tool == "eraser";

            // 1. CHẾ ĐỘ VẼ HÌNH (SHAPE MODE)
            if (isShapeDrawing)
            {
                if (_currentTempStroke != null)
                {
                    MyCanvas.Strokes.Remove(_currentTempStroke);
                }

                StylusPointCollection points = null;
                if (penType == "square" || penType == "rectangle")
                    points = CreateRectanglePoints(_startPoint, currentPoint);
                else if (penType == "circle" || penType == "ellipse")
                    points = CreateEllipsePoints(_startPoint, currentPoint);
                else if (penType == "triangle")
                    points = CreateTrianglePoints(_startPoint, currentPoint);
                else if (penType == "line")
                    points = CreateLinePoints(_startPoint, currentPoint);

                if (points != null)
                {
                    _currentTempStroke = new Stroke(points)
                    {
                        // Khởi tạo thuộc tính riêng cho Shape để không dính tới bút
                        DrawingAttributes = new DrawingAttributes
                        {
                            Color = (Color)ColorConverter.ConvertFromString(_viewModel.Toolbar.CurrentShapeColor),
                            Width = _viewModel.Toolbar.CurrentShapeThickness,
                            Height = _viewModel.Toolbar.CurrentShapeThickness,
                            FitToCurve = false,
                            IgnorePressure = true
                        }
                    };
                    MyCanvas.Strokes.Add(_currentTempStroke);
                }
                return;
            }

            // 2. CHẾ ĐỘ TẨY (ERASER)
            if (isEraser)
            {
                // Xóa trên máy cục bộ
                MyCanvas.Strokes.Erase(
                    new Point[] { lastPoint, currentPoint },
                    new EllipseStylusShape(_viewModel.Toolbar.EraserSize, _viewModel.Toolbar.EraserSize));

                // Gửi lệnh xóa cho người khác
                DrawNetworkLine(lastPoint, currentPoint, _viewModel.Toolbar.CurrentColor, _viewModel.Toolbar.CurrentThickness);
                _viewModel.SendDrawData(lastPoint, currentPoint);
                lastPoint = currentPoint;

                UpdateEraserCursor(currentPoint);
                return;
            }

            // 3. CHẾ ĐỘ VẼ BÚT THƯỜNG (NORMAL DRAW / PENCIL)
            if (_viewModel.Toolbar.IsPencilSelected && e.LeftButton == MouseButtonState.Pressed)
            {
                // Gửi dữ liệu vẽ đi (Hàm này trong ViewModel sẽ lo việc đóng gói JSON và gửi qua Socket)
                _viewModel.SendDrawData(lastPoint, currentPoint);

                // Cập nhật điểm cuối cho đoạn vẽ tiếp theo
                lastPoint = currentPoint;
            }

            // OCR
            if (_viewModel.SelectedTool?.ToLowerInvariant() == "ocr" && _ocrSelectionRect != null)
            {
                var x = Math.Min(currentPoint.X, _ocrStartPoint.X);
                var y = Math.Min(currentPoint.Y, _ocrStartPoint.Y);
                var w = Math.Max(currentPoint.X, _ocrStartPoint.X) - x;
                var h = Math.Max(currentPoint.Y, _ocrStartPoint.Y) - y;

                _ocrSelectionRect.Width = w;
                _ocrSelectionRect.Height = h;
                System.Windows.Controls.Canvas.SetLeft(_ocrSelectionRect, x);
                System.Windows.Controls.Canvas.SetTop(_ocrSelectionRect, y);
                return;
            }
        }
        private async void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // NORMAL DRAW / ERASER
            if (isDrawing)
            {
                isDrawing = false;

                if (EraserCursor != null)
                {
                    EraserCursor.Visibility = Visibility.Collapsed;
                }

                _currentTempStroke = null;

                if (MyCanvas.IsMouseCaptured)
                {
                    MyCanvas.ReleaseMouseCapture();
                }
            }

            // SHAPE DRAW
            if (isShapeDrawing)
            {
                Point endPoint = e.GetPosition(MyCanvas);

                string penType =
                    _viewModel?.Toolbar?.CurrentPenType?.ToLowerInvariant();

                // XÓA PREVIEW STROKE CŨ
                _currentTempStroke = null;
                //vẽ local
                // GỬI QUA SERVER
                ClientSocket.Instance.Send(new DrawMessage
                {
                    type = "SHAPE",

                    roomId = _viewModel.RoomId,

                    userId = ClientSocket.Instance.CurrentUserId,

                    username = ClientSocket.Instance.CurrentUsername,

                    shapeType = penType,

                    x1 = _startPoint.X,
                    y1 = _startPoint.Y,

                    x2 = endPoint.X,
                    y2 = endPoint.Y,

                    color = _viewModel.Toolbar.CurrentShapeColor,
                    thickness = _viewModel.Toolbar.CurrentShapeThickness
                });

                isShapeDrawing = false;

                _currentTempStroke = null;

                if (MyCanvas.IsMouseCaptured)
                {
                    MyCanvas.ReleaseMouseCapture();
                }
            }

            // OCR
            if (_viewModel.SelectedTool?.ToLowerInvariant() == "ocr" && _ocrSelectionRect != null)
            {
                if (MyCanvas.IsMouseCaptured) MyCanvas.ReleaseMouseCapture();

                // Lấy tọa độ và kích thước khung chọn
                int x = (int)System.Windows.Controls.Canvas.GetLeft(_ocrSelectionRect);
                int y = (int)System.Windows.Controls.Canvas.GetTop(_ocrSelectionRect);
                int width = (int)_ocrSelectionRect.Width;
                int height = (int)_ocrSelectionRect.Height;

                OverlayCanvas.Children.Remove(_ocrSelectionRect);
                _ocrSelectionRect = null;

                if (width > 10 && height > 10) // Bỏ qua nếu khung quá nhỏ (click nhầm)
                {
                    try
                    {
                        // 1. Chụp màn hình InkCanvas
                        RenderTargetBitmap rtb = new RenderTargetBitmap((int)MyCanvas.ActualWidth, (int)MyCanvas.ActualHeight, 96d, 96d, PixelFormats.Default);
                        rtb.Render(MyCanvas);

                        // 2. Cắt đúng vùng người dùng chọn
                        CroppedBitmap crop = new CroppedBitmap(rtb, new Int32Rect(x, y, width, height));

                        // 3. Chuyển thành Base64
                        string base64String = "";
                        using (MemoryStream ms = new MemoryStream())
                        {
                            BitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(crop));
                            encoder.Save(ms);
                            byte[] imageBytes = ms.ToArray();
                            base64String = Convert.ToBase64String(imageBytes);
                        }

                        // Đổi trỏ chuột thành Loading để user chờ
                        Mouse.OverrideCursor = Cursors.Wait;

                        // 4. Gọi API
                        string detectedText = await OcrService.RecognizeTextAsync(base64String);

                        Mouse.OverrideCursor = null;

                        // 5. In chữ ra màn hình và đồng bộ Socket
                        if (!string.IsNullOrEmpty(detectedText))
                        {
                            // Vẽ TextBlock lên màn hình và gửi cho client khác
                            _viewModel.SendText(detectedText, new Point(x, y));

                            // Xóa các nét chữ viết tay cũ bên dưới vùng chọn
                            MyCanvas.Strokes.Erase(new Rect(x, y, width, height)); 
                        }
                        else
                        {
                            MessageBox.Show("Không tìm thấy chữ nào trong vùng vừa chọn!", "OCR Magic", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        Mouse.OverrideCursor = null;
                        MessageBox.Show("Lỗi cắt ảnh: " + ex.Message);
                    }
                }
                return;
            }
        }

        private void UpdateEraserCursor(Point p)
        {
            if (EraserCursor != null)
            {
                double halfSize = _viewModel.Toolbar.CurrentThickness / 2;

                EraserCursor.Margin =
                    new Thickness(
                        p.X - halfSize,
                        p.Y - halfSize,
                        0,
                        0);
            }
        }

        private void DrawLineLocal(
            Point p1,
            Point p2,
            string hexColor,
            double thickness)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hexColor))
                {
                    hexColor = "#000000";
                }

                // REMOTE ERASER
                if (hexColor == "#ERASE")
                {
                    var eraserShape =
                        new EllipseStylusShape(thickness, thickness);

                    MyCanvas.Strokes.Erase(
                        new Point[] { p1, p2 },
                        eraserShape);

                    return;
                }

                StylusPointCollection points =
                    new StylusPointCollection
                    {
                        new StylusPoint(p1.X, p1.Y),
                        new StylusPoint(p2.X, p2.Y)
                    };

                Color parsedColor =
                    (Color)ColorConverter.ConvertFromString(hexColor);

                Stroke stroke = new Stroke(points)
                {
                    DrawingAttributes = new DrawingAttributes
                    {
                        Color = parsedColor,
                        Width = thickness,
                        Height = thickness,
                        FitToCurve = true,
                        IgnorePressure = true
                    }
                };

                MyCanvas.Strokes.Add(stroke);
            }
            catch (Exception ex)
            {
                Console.WriteLine("DrawLineLocal error: " + ex.Message);
            }
        }

        // Trong Canvas.xaml.cs - Tìm các phương thức vẽ từ mạng (ví dụ DrawNetworkLine)
        private void DrawNetworkLine(Point start, Point end, string colorHex, double thickness)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    Color color;

                    // 🔥 FIX CASE ERASER
                    if (string.IsNullOrWhiteSpace(colorHex) || colorHex == "#ERASE")
                    {
                        color = Colors.Transparent;
                    }
                    else
                    {
                        color = (Color)ColorConverter.ConvertFromString(colorHex);
                    }

                    var attributes = new DrawingAttributes
                    {
                        Color = color,
                        Width = thickness,
                        Height = thickness,
                        FitToCurve = true
                    };

                    var points = new StylusPointCollection
            {
                new StylusPoint(start.X, start.Y),
                new StylusPoint(end.X, end.Y)
            };

                    var stroke = new Stroke(points, attributes);

                    MyCanvas.Strokes.Add(stroke);
                }
                catch (FormatException)
                {
                    // 🔥 fallback an toàn tuyệt đối
                    var fallbackColor = Colors.Black;

                    var attributes = new DrawingAttributes
                    {
                        Color = fallbackColor,
                        Width = thickness,
                        Height = thickness,
                        FitToCurve = true
                    };

                    var points = new StylusPointCollection
            {
                new StylusPoint(start.X, start.Y),
                new StylusPoint(end.X, end.Y)
            };

                    MyCanvas.Strokes.Add(new Stroke(points, attributes));
                }
            });
        }
        private void DrawShape(DrawMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StylusPointCollection points = null;

                Point start = new Point(msg.x1, msg.y1);
                Point end = new Point(msg.x2, msg.y2);

                switch (msg.shapeType?.ToLower())
                {
                    case "rectangle":
                    case "square":
                        points = CreateRectanglePoints(start, end);
                        break;

                    case "ellipse":
                    case "circle":
                        points = CreateEllipsePoints(start, end);
                        break;

                    case "triangle":
                        points = CreateTrianglePoints(start, end);
                        break;

                    case "line":
                        points = CreateLinePoints(start, end);
                        break;
                }

                if (points == null) return;

                Stroke stroke = new Stroke(points)
                {
                    DrawingAttributes = new DrawingAttributes
                    {
                        Color = (Color)ColorConverter.ConvertFromString(msg.color),
                        Width = msg.thickness,
                        Height = msg.thickness,
                        FitToCurve = false,
                        IgnorePressure = true
                    }
                };

                MyCanvas.Strokes.Add(stroke);
            });
        }
        private void DrawText(DrawMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TextBlock tb = new TextBlock
                {
                    Text = msg.text,
                    FontSize = msg.fontSize,
                    Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(msg.color))
                };

                InkCanvas.SetLeft(tb, msg.x1);
                InkCanvas.SetTop(tb, msg.y1);

                MyCanvas.Children.Add(tb);
            });
        }

        // Hàm mở bảng màu khi click dấu (+)
        private void OpenColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = colorDialog.Color;
                string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";

                if (DataContext is CanvasViewModel vm)
                {
                    // Gọi hàm thêm màu mới (vừa lưu vừa chọn)
                    vm.Toolbar.AddCustomColor(hex);
                    UpdateCurrentDrawingAttributes(vm);
                }
            }
        }
        // vẽ hình
        private StylusPointCollection CreateRectanglePoints(Point start, Point end)
        {
            var points = new StylusPointCollection();
            // Vẽ theo hình chữ nhật: 4 góc khép kín
            points.Add(new StylusPoint(start.X, start.Y));
            points.Add(new StylusPoint(end.X, start.Y));
            points.Add(new StylusPoint(end.X, end.Y));
            points.Add(new StylusPoint(start.X, end.Y));
            points.Add(new StylusPoint(start.X, start.Y));
            return points;
        }

        private StylusPointCollection CreateEllipsePoints(Point start, Point end)
        {
            var points = new StylusPointCollection();
            double radiusX = Math.Abs(end.X - start.X) / 2;
            double radiusY = Math.Abs(end.Y - start.Y) / 2;
            double centerX = Math.Min(start.X, end.X) + radiusX;
            double centerY = Math.Min(start.Y, end.Y) + radiusY;

            // Giảm khoảng cách góc xuống 5 để nét dày và khít hơn
            for (int i = 0; i <= 360; i += 5)
            {
                double angle = i * Math.PI / 180;
                double x = centerX + radiusX * Math.Cos(angle);
                double y = centerY + radiusY * Math.Sin(angle);
                points.Add(new StylusPoint(x, y));
            }

            // Đảm bảo điểm kết thúc luôn trùng khít 100% với điểm đầu tiên để đóng kín vòng
            points.Add(new StylusPoint(centerX + radiusX, centerY));

            return points;
        }
        private StylusPointCollection CreateLinePoints(Point start, Point end)
        {
            var points = new StylusPointCollection();
            // Đường thẳng chỉ cần 2 điểm: Điểm bắt đầu và Điểm kết thúc
            points.Add(new StylusPoint(start.X, start.Y));
            points.Add(new StylusPoint(end.X, end.Y));
            return points;
        }

        private StylusPointCollection CreateTrianglePoints(Point start, Point end)
        {
            var points = new StylusPointCollection();

            // Vẽ tam giác cân hướng lên: 
            // Đỉnh nằm ở giữa cạnh trên, 2 góc ở dưới
            double topX = start.X + (end.X - start.X) / 2;
            double topY = start.Y;

            points.Add(new StylusPoint(topX, topY));       // 1. Đỉnh trên cùng
            points.Add(new StylusPoint(end.X, end.Y));     // 2. Góc dưới cùng bên phải
            points.Add(new StylusPoint(start.X, end.Y));   // 3. Góc dưới cùng bên trái
            points.Add(new StylusPoint(topX, topY));       // 4. Vòng lại đỉnh trên để khép kín hình

            return points;
        }

        private void txtChatInput_KeyDown(
            object sender,
            KeyEventArgs e)
                {
                    if (e.Key == Key.Enter)
                    {
                        if (DataContext is CanvasViewModel vm)
                        {
                            vm.SendChatMessageCommand.Execute(null);
                        }

                        e.Handled = true;
                    }
                }
        }
        
        
    }