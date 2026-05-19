using DrawClient.Models;
using DrawClient.ViewModels.Canvas;
using DrawClient.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Threading.Tasks;

namespace DrawClient.ViewModels
{
    public class UserParticipant
    {
        public string Initials { get; set; } = "";
        public string ColorHex { get; set; } = "";
    }

    public class CanvasViewModel : INotifyPropertyChanged
    {
        public ToolbarViewModel Toolbar { get; set; } = new ToolbarViewModel();

        public Action<Point, Point, string, double> OnLineReceived;
        public Action<DrawMessage> OnShapeReceived;
        public Action<DrawMessage> OnTextReceived;
        public Action OnCanvasCleared;
        public Action GoBackToLobby;
        public UndoRedoManager UndoRedoManager { get; private set; } = new UndoRedoManager();
        public event Action OnUndoRedo; 
        private bool _isCleanedUp = false;


        #region Properties
        private string _roomName;
        public string RoomName { get => _roomName; set { _roomName = value; OnPropertyChanged(); } }

        private string _roomId;
        public string RoomId { get => _roomId; set { _roomId = value; OnPropertyChanged(); } }

        private string _roomPassword;
        public string RoomPassword { get => _roomPassword; set { _roomPassword = value; OnPropertyChanged(); } }

        private bool _isColorMenuOpen;
        public bool IsColorMenuOpen { get => _isColorMenuOpen; set { _isColorMenuOpen = value; OnPropertyChanged(); } }

        private bool _isPenMenuOpen;
        public bool IsPenMenuOpen { get => _isPenMenuOpen; set { _isPenMenuOpen = value; OnPropertyChanged(); } }

        private string _currentPenType = "Brush";
        public string CurrentPenType { get => _currentPenType; set { _currentPenType = value; OnPropertyChanged(); } }

        private InkCanvasEditingMode _currentEditingMode = InkCanvasEditingMode.Select;
        public InkCanvasEditingMode CurrentEditingMode { get => _currentEditingMode; set { _currentEditingMode = value; OnPropertyChanged(); } }

        private bool _isSidebarOpen = false;
        public GridLength RightSidebarWidth => _isSidebarOpen ? new GridLength(320) : new GridLength(0);

        private bool _isProfilePopoverVisible;
        public bool IsProfilePopoverVisible { get => _isProfilePopoverVisible; set { _isProfilePopoverVisible = value; OnPropertyChanged(); } }

        private string _currentColor = "#000000";
        public string CurrentColor { get => _currentColor; set { _currentColor = value; OnPropertyChanged(); } }

        private double _penThickness = 1.0;
        public double PenThickness
        {
            get => _penThickness;
            set
            {
                _penThickness = value;
                OnPropertyChanged();
                if (IsPenTool(Toolbar.CurrentPenType)) Toolbar.CurrentThickness = value;
            }
        }

        private double _eraserThickness = 20.0;
        public double EraserThickness
        {
            get => _eraserThickness;
            set
            {
                _eraserThickness = value;
                OnPropertyChanged();
                if (SelectedTool?.ToLower() == "eraser") Toolbar.CurrentThickness = value;
            }
        }

        private string _currentUserInitials;
        public string CurrentUserInitials { get => _currentUserInitials; set { _currentUserInitials = value; OnPropertyChanged(); } }

        private string _selectedTool = "select";
        public string SelectedTool
        {
            get => _selectedTool;
            set
            {
                _selectedTool = value;
                OnPropertyChanged();
                Toolbar.CurrentThickness = (_selectedTool?.ToLower() == "eraser") ? EraserThickness : PenThickness;
            }
        }

        private string _currentShape = "rectangle"; // Mặc định là hình chữ nhật
        public string CurrentShape
        {
            get => _currentShape;
            set { _currentShape = value; OnPropertyChanged(); }
        }

        private string _previousColor = "#000000";
        
        private bool _isOcrToastVisible = false;

        public bool IsOcrToastVisible
        {
            get => _isOcrToastVisible;
            set { _isOcrToastVisible = value; OnPropertyChanged(); }
        }

        private bool _canUndo = false;
                public bool CanUndo
                {
                    get => _canUndo;
                    set { _canUndo = value; OnPropertyChanged(); }
                }

                private bool _canRedo = false;
                public bool CanRedo
                {
                    get => _canRedo;
                    set { _canRedo = value; OnPropertyChanged(); }
                }

                private string _historyInfo = "History: 0 Undo | 0 Redo";
                public string HistoryInfo
                {
                    get => _historyInfo;
                    set { _historyInfo = value; OnPropertyChanged(); }
                }

        #endregion

        #region Commands
        public ICommand LeaveRoomCommand { get; }
        public ICommand ShowRoomInfoCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand ToggleProfilePopoverCommand { get; }
        public ICommand AccountManagerCommand { get; }
        public ICommand SelectToolCommand { get; }
        public ICommand ChooseColorCommand { get; }
        public ICommand ClearCanvasCommand { get; }
        public ICommand ToggleColorMenuCommand { get; }
        public ICommand TogglePenMenuCommand { get; }
        public ICommand ChangeColorCommand { get; }
        public ICommand ChangePenTypeCommand { get; }
        public ICommand ChangeThicknessCommand { get; }
        public ICommand ChangeShapeCommand { get; } // Lệnh đổi hình dạng

        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        
        public ICommand SendChatMessageCommand { get; }

        #endregion
        public ObservableCollection<UserParticipant> Users { get; set; }
        public ObservableCollection<string> NetworkLogs { get; set; }
        public ObservableCollection<ChatMessage> ChatMessages { get; set; }

        private bool _socketInitialized = false;

        public CanvasViewModel(string roomName, string roomId, string password = "")
        {
            _roomName = roomName;
            _roomId = roomId;
            _roomPassword = string.IsNullOrEmpty(password)
                ? "Không có mật khẩu"
                : password;

            Toolbar.ToolSelected += (sender, toolType) =>
            {
                ExecuteSelectTool(toolType);
            };

            InitSocketListener();

            LeaveRoomCommand = new RelayCommand(ExecuteLeaveRoom);
            ShowRoomInfoCommand = new RelayCommand(ExecuteShowRoomInfo);

            ToggleSidebarCommand = new RelayCommand(_ =>
            {
                _isSidebarOpen = !_isSidebarOpen;
                OnPropertyChanged(nameof(RightSidebarWidth));
            });

            ToggleProfilePopoverCommand = new RelayCommand(_ =>
            {
                IsProfilePopoverVisible = !IsProfilePopoverVisible;
            });

            AccountManagerCommand = new RelayCommand(_ =>
            {
                MessageBox.Show(
                    "Open Account Manager",
                    "Account",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                IsProfilePopoverVisible = false;
            });

            SelectToolCommand = new RelayCommand(ExecuteSelectTool);

            ChooseColorCommand = new RelayCommand(param =>
            {
                if (param != null)
                {
                    CurrentColor = param.ToString();

                    SelectedTool = "pen";
                    CurrentEditingMode = InkCanvasEditingMode.Ink;

                    if (CurrentColor != "#FFFFFF")
                    {
                        _previousColor = CurrentColor;
                    }
                }
            });

            ClearCanvasCommand = new RelayCommand(ExecuteClearCanvas);
            // Undo/Redo
            UndoRedoManager.OnUndo += HandleUndo;  
            UndoRedoManager.OnRedo += HandleRedo;  
            UndoRedoManager.OnHistoryChanged += UpdateHistoryUI;
            UpdateHistoryUI();

            UndoCommand = new RelayCommand(_ => ExecuteUndo(), _ => CanUndo);
            RedoCommand = new RelayCommand(_ => ExecuteRedo(), _ => CanRedo);
            ClearHistoryCommand = new RelayCommand(_ => ExecuteClearHistory());

            string safeUsername =
                LoginViewModel.CurrentUsername
                ?? ClientSocket.Instance.CurrentUsername
                ?? "U";

            CurrentUserInitials = GetInitials(safeUsername);

            Users = new ObservableCollection<UserParticipant>
            {
                new UserParticipant
                {
                    Initials = CurrentUserInitials,
                    ColorHex = "#1A73E8"
                }
            };

            NetworkLogs = new ObservableCollection<string>
            {
                $"Joined Room: {roomName}",
                $"ID: {roomId}",
                $"Password: {RoomPassword}"
            };

            ChatMessages = new ObservableCollection<ChatMessage>();

            ToggleColorMenuCommand = new RelayCommand(o =>
            {
                IsColorMenuOpen = !IsColorMenuOpen;

                if (IsColorMenuOpen)
                    IsPenMenuOpen = false;
            });

            TogglePenMenuCommand = new RelayCommand(o =>
            {
                IsPenMenuOpen = !IsPenMenuOpen;

                if (IsPenMenuOpen)
                    IsColorMenuOpen = false;
            });

            ChangeColorCommand = new RelayCommand(colorHex =>
            {
                if (colorHex is string hex)
                {
                    CurrentColor = hex;
                    Toolbar.CurrentColor = hex;

                    SelectedTool = "pen";
                    Toolbar.IsPencilSelected = true;  
                    Toolbar.IsEraserSelected = false;
                    CurrentEditingMode = InkCanvasEditingMode.Ink;
                }
            });

            ChangePenTypeCommand = new RelayCommand(penType =>
            {
                if (penType is string type)
                {
                    CurrentPenType = type;
                    Toolbar.CurrentPenType = type;

                    var shapes = new[] { "rectangle", "circle", "triangle", "line", "square", "ellipse" };
                    if (shapes.Any(s => string.Equals(s, type, StringComparison.OrdinalIgnoreCase)))
                    {
                        CurrentShape = type;
                        SelectedTool = "shape";
                        CurrentEditingMode = InkCanvasEditingMode.None;
                    }
                    else
                    {
                        SelectedTool = "pen";
                        Toolbar.IsPencilSelected = true;
                        Toolbar.IsEraserSelected = false;
                        CurrentEditingMode = InkCanvasEditingMode.Ink;
                    }
                    IsPenMenuOpen = false;
                }
            });
            ChangeThicknessCommand = new RelayCommand(thickness =>
            {
                if (double.TryParse(thickness.ToString(), out double t))
                {
                    if (SelectedTool?.ToLower() == "eraser")
                    {
                        EraserThickness = t;
                        Toolbar.EraserSize = t;
                    }
                    else
                    {
                        PenThickness = t;
                        Toolbar.PencilSize = t;
                    }
                }
            });
            ChangeShapeCommand = new RelayCommand(param =>
            {
                if (param != null)
                {
                    CurrentShape = param.ToString();
                    SelectedTool = "shape"; // Tự động chuyển sang mode hình dạng
                    CurrentEditingMode = InkCanvasEditingMode.None;
                }
            });

            SendChatMessageCommand = new RelayCommand(_ => ExecuteSendChatMessage());
        }

        private void ExecuteSelectTool(object obj)
        {
            string tool = obj?.ToString()?.ToLower() ?? "pen";
            // Ngăn không cho các event cập nhật thuộc tính ghi đè lên SelectedTool
            if (tool == "sizechanged" || tool == "colorchanged" || tool == "pentypechanged")
            {
                return;
            }
            if (tool == "color")
            {
                IsColorMenuOpen = !IsColorMenuOpen;

                if (IsColorMenuOpen)
                    IsPenMenuOpen = false;

                return;
            }
            

            if (SelectedTool == tool)
            {
                IsColorMenuOpen = false;
                IsPenMenuOpen = false;

                if (tool == "select")
                {
                    CurrentEditingMode = InkCanvasEditingMode.Select;
                }
                if (tool == "ocr")
                {
                    ShowOcrToastTemporarily();
                }
                return; 
            }

            SelectedTool = tool;
            IsColorMenuOpen = false;
            IsPenMenuOpen = false; ;

            if (tool.ToLowerInvariant() != "ocr")
            {
                IsOcrToastVisible = false;
                _ocrToastToken++; 
            }

            switch (tool)
            {
                case "select":
                    CurrentEditingMode = InkCanvasEditingMode.Select;
                    break;

                case "pencil":
                case "pen":
                    CurrentEditingMode = InkCanvasEditingMode.Ink;
                    Toolbar.IsPencilSelected = true;   
                    Toolbar.IsEraserSelected = false;
                    Toolbar.IsShapeSelected = false;
                    Toolbar.IsTextSelected = false;
                    Toolbar.CurrentColor = CurrentColor;
                    Toolbar.CurrentThickness = Toolbar.PencilSize;
                    if (string.IsNullOrEmpty(Toolbar.CurrentPenType) || IsShapeTool(Toolbar.CurrentPenType))
                    {
                        Toolbar.CurrentPenType = "brush";
                    }
                    break;

                case "eraser":
                    CurrentEditingMode = InkCanvasEditingMode.EraseByPoint;
                    Toolbar.IsEraserSelected = true;   
                    Toolbar.IsPencilSelected = false;
                    Toolbar.IsShapeSelected = false;
                    Toolbar.IsTextSelected = false;
                    Toolbar.CurrentPenType = "eraser";
                    Toolbar.CurrentThickness = Toolbar.EraserSize;
                    break;
                case "shape":
                    CurrentEditingMode = InkCanvasEditingMode.None;
                    Toolbar.IsPencilSelected = false;
                    Toolbar.IsEraserSelected = false;
                    Toolbar.IsShapeSelected = true;
                    Toolbar.IsTextSelected = false;
                    Toolbar.CurrentThickness = Toolbar.CurrentShapeThickness;

                    var shapes = new System.Collections.Generic.List<string> { "square", "circle", "triangle", "line", "rectangle", "ellipse" };
                    if (!shapes.Contains(Toolbar.CurrentPenType?.ToLowerInvariant()))
                    {
                        Toolbar.CurrentPenType = "rectangle";
                    }
                    break;
                case "ocr":
                    CurrentEditingMode = InkCanvasEditingMode.None;

                    if (Toolbar != null)
                    {
                        Toolbar.IsPencilSelected = false;
                        Toolbar.IsShapeSelected = false;
                        Toolbar.IsEraserSelected = false;
                        Toolbar.IsTextSelected = false;
                    }

                    ShowOcrToastTemporarily();
                    break;
            }
        }
        private bool IsShapeTool(string tool)
        {
            var shapes = new System.Collections.Generic.List<string> { "square", "rectangle", "circle", "ellipse", "triangle", "line" };
            return shapes.Contains(tool?.ToLowerInvariant());
        }

        private int _ocrToastToken = 0;

        private void ShowOcrToastTemporarily()
        {
            int currentToken = ++_ocrToastToken;

            IsOcrToastVisible = false;

            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (currentToken != _ocrToastToken) return;
                IsOcrToastVisible = true;
                await Task.Delay(1500);

                if (currentToken == _ocrToastToken)
                {
                    IsOcrToastVisible = false;
                }
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void InitSocketListener()
        {
            if (_socketInitialized) return;

            _socketInitialized = true;

            ClientSocket.Instance.OnMessageReceived -= HandleSocketMessage;
            ClientSocket.Instance.OnMessageReceived += HandleSocketMessage;
        }

        private readonly JsonSerializerOptions _jsonOptions =
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private void HandleSocketMessage(string msg)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(msg))
                {
                    if (!doc.RootElement.TryGetProperty("type", out var typeEl))
                        return;

                    string type = typeEl.GetString();

                    if (string.IsNullOrEmpty(type))
                        return;

                    // ================= HISTORY =================
                    if (type == "HISTORY")
                    {
                        if (!doc.RootElement.TryGetProperty("actions", out var actions))
                            return;

                        foreach (var item in actions.EnumerateArray())
                        {
                            var draw = JsonSerializer.Deserialize<DrawMessage>(
                                item.GetRawText(),
                                _jsonOptions);

                            if (draw == null)
                                continue;

                            DispatchDraw(draw);

                            // Lưu history vào Undo stack
                            if (draw.type == "DRAW" ||
                                draw.type == "ERASE" ||
                                draw.type == "SHAPE" ||
                                draw.type == "TEXT")
                            {
                                UndoRedoManager.AddAction(
                                    new DrawAction(
                                        draw.type,
                                        new Point(draw.x1, draw.y1),
                                        new Point(draw.x2, draw.y2),
                                        draw.color,
                                        draw.thickness,
                                        draw.userId,
                                        draw.username,
                                        RoomId
                                    )
                                );
                            }
                        }

                        return;
                    }

                    // ================= CHAT HISTORY =================
                    if (type == "CHAT_HISTORY")
                    {
                        if (!doc.RootElement.TryGetProperty("messages", out var messages))
                            return;

                        foreach (var item in messages.EnumerateArray())
                        {
                            var chat = JsonSerializer.Deserialize<DrawMessage>(
                                item.GetRawText(),
                                _jsonOptions);

                            if (chat == null)
                                continue;

                            DispatchDraw(chat);
                        }

                        return;
                    }

                    // ================= NORMAL MESSAGE =================
                    var drawMsg = JsonSerializer.Deserialize<DrawMessage>(
                        msg,
                        _jsonOptions);

                    if (drawMsg == null)
                        return;

                    switch (drawMsg.type)
                    {
                        // ================= DRAW =================
                        case "DRAW":

                            DispatchDraw(drawMsg);

                            // Không add duplicate action của chính mình
                            if (drawMsg.userId != ClientSocket.Instance.CurrentUserId)
                            {
                                UndoRedoManager.AddAction(
                                    new DrawAction(
                                        "DRAW",
                                        new Point(drawMsg.x1, drawMsg.y1),
                                        new Point(drawMsg.x2, drawMsg.y2),
                                        drawMsg.color,
                                        drawMsg.thickness,
                                        drawMsg.userId,
                                        drawMsg.username,
                                        RoomId
                                    )
                                );
                            }

                            break;

                        // ================= ERASE =================
                        case "ERASE":

                            DispatchDraw(drawMsg);

                            if (drawMsg.userId != ClientSocket.Instance.CurrentUserId)
                            {
                                UndoRedoManager.AddAction(
                                    new DrawAction(
                                        "ERASE",
                                        new Point(drawMsg.x1, drawMsg.y1),
                                        new Point(drawMsg.x2, drawMsg.y2),
                                        "#ERASE",
                                        drawMsg.thickness,
                                        drawMsg.userId,
                                        drawMsg.username,
                                        RoomId
                                    )
                                );
                            }

                            break;

                        // ================= SHAPE =================
                        case "SHAPE":

                            DispatchDraw(drawMsg);

                            if (drawMsg.userId != ClientSocket.Instance.CurrentUserId)
                            {
                                UndoRedoManager.AddAction(
                                    new DrawAction(
                                        "SHAPE",
                                        new Point(drawMsg.x1, drawMsg.y1),
                                        new Point(drawMsg.x2, drawMsg.y2),
                                        drawMsg.color,
                                        drawMsg.thickness,
                                        drawMsg.userId,
                                        drawMsg.username,
                                        RoomId
                                    )
                                );
                            }

                            break;

                        // ================= TEXT =================
                        case "TEXT":

                            DispatchDraw(drawMsg);

                            if (drawMsg.userId != ClientSocket.Instance.CurrentUserId)
                            {
                                UndoRedoManager.AddAction(
                                    new DrawAction(
                                        "TEXT",
                                        new Point(drawMsg.x1, drawMsg.y1),
                                        new Point(drawMsg.x2, drawMsg.y2),
                                        drawMsg.color,
                                        drawMsg.thickness,
                                        drawMsg.userId,
                                        drawMsg.username,
                                        RoomId
                                    )
                                );
                            }

                            break;

                        // ================= UNDO =================
                        case "UNDO":

                            InvokeUI(() =>
                            {
                                if (UndoRedoManager.CanUndo())
                                {
                                    UndoRedoManager.Undo();

                                    UpdateHistoryUI();

                                    Console.WriteLine(
                                        $"[NETWORK] Undo from {drawMsg.username}");
                                }
                            });

                            break;

                        // ================= REDO =================
                        case "REDO":

                            InvokeUI(() =>
                            {
                                if (UndoRedoManager.CanRedo())
                                {
                                    UndoRedoManager.Redo();

                                    UpdateHistoryUI();

                                    Console.WriteLine(
                                        $"[NETWORK] Redo from {drawMsg.username}");
                                }
                            });

                            break;

                        // ================= OTHERS =================
                        default:
                            DispatchDraw(drawMsg);
                            break;
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine("JSON parse error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Socket parse error: " + ex);
            }
        }

        private void DispatchDraw(DrawMessage draw)
        {
            switch (draw.type)
            {
                case "JOIN":
                    InvokeUI(() =>
                    {
                        // Nếu gói tin JOIN này là của chính mình thì bỏ qua (vì constructor đã tự add mình rồi)
                        if (draw.userId == ClientSocket.Instance.CurrentUserId)
                            return;

                        string joinedUsername = string.IsNullOrEmpty(draw.username) ? $"User {draw.userId}" : draw.username;
                        string initials = GetInitials(joinedUsername);

                        // Kiểm tra trùng lặp trước khi thêm vào danh sách Sidebar bên phải
                        if (!Users.Any(u => u.Initials == initials))
                        {
                            Users.Add(new UserParticipant
                            {
                                Initials = initials,
                                ColorHex = "#4CAF50" // Đặt màu đại diện cho người khác
                            });

                            // Thêm một dòng thông báo hệ thống lên góc màn hình vẽ
                            NetworkLogs.Add($"[MẠNG] {joinedUsername} đã vào phòng.");
                        }
                    });
                    break;
                case "DRAW":
                    InvokeUI(() =>
                        OnLineReceived?.Invoke(
                            new Point(draw.x1, draw.y1),
                            new Point(draw.x2, draw.y2),
                            draw.color,
                            draw.thickness));
                    break;

                case "ERASE":
                    InvokeUI(() =>
                        OnLineReceived?.Invoke(
                            new Point(draw.x1, draw.y1),
                            new Point(draw.x2, draw.y2),
                            "#ERASE",
                            draw.thickness));
                    break;

                // 
                case "UNDO":
                    InvokeUI(() =>
                    {
                        if (UndoRedoManager.CanUndo())
                        {
                            UndoRedoManager.Undo();
                            UpdateHistoryUI();
                        }
                    });
                    break;

                case "REDO":
                    InvokeUI(() =>
                    {
                        if (UndoRedoManager.CanRedo())
                        {
                            UndoRedoManager.Redo();
                            UpdateHistoryUI();
                        }
                    });
                    break;

                case "SHAPE":
                    InvokeUI(() => OnShapeReceived?.Invoke(draw));
                    break;

                case "TEXT":
                    InvokeUI(() => OnTextReceived?.Invoke(draw));
                    break;

                case "CLEAR":
                    InvokeUI(() => OnCanvasCleared?.Invoke());
                    break;

                case "LEAVE":
                    InvokeUI(() =>
                    {
                        string leftUsername = string.IsNullOrEmpty(draw.username) ? $"User {draw.userId}" : draw.username;

                        // Ghi nhận log mạng hiển thị lên góc màn hình phòng vẽ
                        NetworkLogs.Add($"[MẠNG] {leftUsername} đã rời phòng.");

                        // Tự động xóa user này ra khỏi danh sách Online Users hiển thị ở Sidebar bên phải
                        string targetInitials = GetInitials(leftUsername);
                        var userToRemove = Users.FirstOrDefault(u => u.Initials == targetInitials);
                        if (userToRemove != null)
                        {
                            Users.Remove(userToRemove);
                        }
                    });
                    break;

                case "CHAT":
                    InvokeUI(() =>
                    {
                        DateTime messageTime =
                            draw.timestamp == default
                                ? DateTime.Now
                                : draw.timestamp;

                        bool showSeparator = false;

                        if (ChatMessages.Count == 0)
                        {
                            showSeparator = true;
                        }
                        else
                        {
                            var last = ChatMessages.Last();

                            bool differentDay =
                                last.Timestamp.Date != messageTime.Date;

                            bool over15Minutes =
                                (messageTime - last.Timestamp).TotalMinutes >= 15;

                            if (differentDay || over15Minutes)
                                showSeparator = true;
                        }

                        ChatMessages.Add(new ChatMessage
                        {
                            User = draw.username,
                            Message = draw.text,
                            Timestamp = messageTime,
                            ShowSeparator = showSeparator,
                            IsMine = draw.userId == ClientSocket.Instance.CurrentUserId
                        });
                    });
                    break;
            }
        }

        private void InvokeUI(Action action)
        {
            Application.Current.Dispatcher.Invoke(action);
        }
        public void SendDrawData(Point p1, Point p2)
        {
            if (Math.Abs(p1.X - p2.X) < 0.5 && Math.Abs(p1.Y - p2.Y) < 0.5)
                return;

            bool isEraser =
                Toolbar.IsEraserSelected ||
                SelectedTool?.ToLower() == "eraser";
            bool isShape = SelectedTool?.ToLower() == "shape";

            var msg = new DrawMessage
            {
                roomId = RoomId,
                userId = ClientSocket.Instance.CurrentUserId, // Đảm bảo lấy ID từ Socket Instance
                username = ClientSocket.Instance.CurrentUsername,
                x1 = p1.X,
                y1 = p1.Y,
                x2 = p2.X,
                y2 = p2.Y,
                thickness = isEraser ? Toolbar.EraserSize : Toolbar.CurrentThickness
            };

            // 3. Gán Type NHẤT QUÁN (Sửa lỗi mục 1)
            if (isEraser)
            {
                msg.type = "ERASE";
                msg.color = "#ERASE";
                msg.thickness = Toolbar.EraserSize;
                UndoRedoManager.AddAction(new DrawAction("ERASE", p1, p2, msg.color, msg.thickness, ClientSocket.Instance.CurrentUserId, ClientSocket.Instance.CurrentUsername, RoomId));
            }
            else if (isShape)
            {
                msg.type = "SHAPE";
                msg.shapeType = CurrentShape;
                msg.color = Toolbar.CurrentColor;
                msg.thickness = Toolbar.PencilSize;
                UndoRedoManager.AddAction(new DrawAction("SHAPE", p1, p2, msg.color, msg.thickness, ClientSocket.Instance.CurrentUserId, ClientSocket.Instance.CurrentUsername, RoomId) { ShapeType = CurrentShape });
            }
            else
            {
                msg.type = "DRAW";
                msg.color = Toolbar.CurrentColor;
                msg.thickness = Toolbar.PencilSize;
                UndoRedoManager.AddAction(new DrawAction("DRAW", p1, p2, msg.color, msg.thickness, ClientSocket.Instance.CurrentUserId, ClientSocket.Instance.CurrentUsername, RoomId));
            }
            if (p1.X == p2.X && p1.Y == p2.Y) return;

            ClientSocket.Instance.Send(msg);
        }
        public void SendText(
    string text,
    Point position)
        {
            ClientSocket.Instance.Send(new DrawMessage
            {
                type = "TEXT",

                roomId = RoomId,

                userId = ClientSocket.Instance.CurrentUserId,

                username = ClientSocket.Instance.CurrentUsername,

                text = text,

                x1 = position.X,
                y1 = position.Y,

                color = Toolbar.CurrentColor,

                fontSize = Toolbar.CurrentThickness * 5
            });
        }
        private void ExecuteClearCanvas(object obj)
        {
            string safeUsername =
                LoginViewModel.CurrentUsername
                ?? ClientSocket.Instance.CurrentUsername
                ?? "Unknown";

            var msg = new DrawMessage
            {
                type = "CLEAR",
                roomId = RoomId,
                userId = ClientSocket.Instance.CurrentUserId,
                username = safeUsername,
            };

            ClientSocket.Instance.Send(msg);

            OnCanvasCleared?.Invoke();
        }

        private void ExecuteShowRoomInfo(object obj)
        {
            MessageBox.Show(
                $"Room ID: {RoomId}\nPassword: {RoomPassword}",
                "Thông tin phòng",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ExecuteLeaveRoom(object obj)
        {
            string safeUsername =
                LoginViewModel.CurrentUsername
                ?? ClientSocket.Instance.CurrentUsername
                ?? "Unknown";

            var leaveMsg = new DrawMessage
            {
                type = "LEAVE",
                roomId = RoomId,
                userId = ClientSocket.Instance.CurrentUserId,
                username = safeUsername,
            };

            ClientSocket.Instance.Send(leaveMsg);

            Cleanup();
            ClientSocket.Instance.Disconnect();

            GoBackToLobby?.Invoke();
        }

        private string GetInitials(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return "U";

            string initials = "";

            string[] parts = username.Trim().Split(' ');

            foreach (string part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    initials += char.ToUpper(part[0]);
                }
            }

            return initials.Length > 2
                ? initials.Substring(0, 2)
                : initials;
        }

        private bool IsPenTool(string tool)
        {
            if (string.IsNullOrWhiteSpace(tool))
                return false;

            string t = tool.Trim().ToLowerInvariant();

            return t == "pencil"
                || t == "brush"
                || t == "fountain"
                || t == "highlighter"
                || t == "laser";
        }

        private void ExecuteUndo()
        {
            if (UndoRedoManager.CanUndo())
            {
                UndoRedoManager.Undo(); // Event sẽ trigger HandleUndo tự động
            }
        }

        private void ExecuteRedo()
        {
            if (UndoRedoManager.CanRedo())
            {
                UndoRedoManager.Redo(); // Event sẽ trigger HandleRedo tự động
            }
        }

        // 🔥 THÊM 2 HÀM MỚI NÀY
        private void HandleUndo(DrawAction undoneAction)
        {
            // 1. Gửi lệnh UNDO lên server
            var undoMsg = new DrawMessage
            {
                type = "UNDO",
                roomId = RoomId,
                userId = ClientSocket.Instance.CurrentUserId,
                username = ClientSocket.Instance.CurrentUsername
            };
            ClientSocket.Instance.Send(undoMsg);
            
            // 2. Vẽ lại canvas từ action stack (trigger OnUndoRedo)
            OnUndoRedo?.Invoke();
            UpdateHistoryUI();
        }

        private void HandleRedo(DrawAction redoAction)
        {
            // 1. Gửi lệnh REDO lên server
            var redoMsg = new DrawMessage
            {
                type = "REDO",
                roomId = RoomId,
                userId = ClientSocket.Instance.CurrentUserId,
                username = ClientSocket.Instance.CurrentUsername
            };
            ClientSocket.Instance.Send(redoMsg);
            
            // 2. Vẽ lại canvas từ action stack
            OnUndoRedo?.Invoke();
            UpdateHistoryUI();
        }

        private void ExecuteClearHistory()
        {
            UndoRedoManager.Clear();
            UpdateHistoryUI();
        }

        private void UpdateHistoryUI()
        {
            CanUndo = UndoRedoManager.CanUndo();
            CanRedo = UndoRedoManager.CanRedo();
            HistoryInfo = $"History: {UndoRedoManager.UndoCount} Undo | {UndoRedoManager.RedoCount} Redo";
        }
        public void Cleanup()
        {
            if (_isCleanedUp)
                return;

            _isCleanedUp = true;

            ClientSocket.Instance.OnMessageReceived -= HandleSocketMessage;
            UndoRedoManager.OnHistoryChanged -= UpdateHistoryUI;   // THÊM DÒNG NÀY
        }

        private string _currentChatMessage;

        public string CurrentChatMessage
        {
            get => _currentChatMessage;
            set
            {
                _currentChatMessage = value;
                OnPropertyChanged();
            }
        }

        private void ExecuteSendChatMessage()
        {
            if (string.IsNullOrWhiteSpace(CurrentChatMessage))
                return;

            ClientSocket.Instance.Send(new DrawMessage
            {
                type = "CHAT",
                roomId = RoomId,
                userId = ClientSocket.Instance.CurrentUserId,
                username = ClientSocket.Instance.CurrentUsername,
                text = CurrentChatMessage.Trim(),
                timestamp = DateTime.Now
            });

            CurrentChatMessage = "";
        }
    }
}