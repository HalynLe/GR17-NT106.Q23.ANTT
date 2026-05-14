using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;

namespace DrawClient.ViewModels.Canvas
{
    public class ToolbarViewModel : INotifyPropertyChanged
    {
        // Sự kiện thông báo cho CanvasViewModel biết công cụ nào đang được chọn
        public event EventHandler<string> ToolSelected;
        public ObservableCollection<string> RecentColors { get; set; } = new ObservableCollection<string>();

        // Trạng thái mở/đóng popup
        private bool _isPencilPopupOpen;
        private bool _isEraserPopupOpen;
        private bool _isShapePopupOpen;
        private bool _isTextPopupOpen;
        private bool _isORCMagicPopupOpen;

        // Popup Pencil
        public bool IsPencilPopupOpen
        {
            get => _isPencilPopupOpen;
            set
            {
                if (_isPencilPopupOpen == value) return;
                _isPencilPopupOpen = value;

                if (value)
                {
                    IsEraserPopupOpen = false;
                    IsShapePopupOpen = false;
                    IsTextPopupOpen = false;
                    IsORCMagicPopupOpen = false;
                }
                OnPropertyChanged();
            }
        }

        // Popup Eraser
        public bool IsEraserPopupOpen
        {
            get => _isEraserPopupOpen;
            set
            {
                if (_isEraserPopupOpen == value) return;
                _isEraserPopupOpen = value;

                if (value)
                {
                    IsPencilPopupOpen = false;
                    IsShapePopupOpen = false;
                    IsTextPopupOpen = false;
                    IsORCMagicPopupOpen = false;
                }
                OnPropertyChanged();
            }
        }

        // Popup Shapes
        public bool IsShapePopupOpen
        {
            get => _isShapePopupOpen;
            set
            {
                if (_isShapePopupOpen == value) return;
                _isShapePopupOpen = value;

                if (value)
                {
                    IsPencilPopupOpen = false;
                    IsEraserPopupOpen = false;
                    IsTextPopupOpen = false;
                    IsORCMagicPopupOpen = false;
                }
                OnPropertyChanged();
            }
        }


        public bool IsTextPopupOpen
        {
            get => _isTextPopupOpen;
            set
            {
                if (_isTextPopupOpen == value) return;
                _isTextPopupOpen = value;

                if (value)
                {
                    IsPencilPopupOpen = false;
                    IsEraserPopupOpen = false;
                    IsShapePopupOpen = false;
                    IsORCMagicPopupOpen = false;
                }
                OnPropertyChanged();
            }
        }

        public bool IsORCMagicPopupOpen
        {
            get => _isORCMagicPopupOpen;
            set
            {
                if (_isORCMagicPopupOpen == value) return;
                _isORCMagicPopupOpen = value;

                if (value)
                {
                    IsPencilPopupOpen = false;
                    IsEraserPopupOpen = false;
                    IsShapePopupOpen = false;
                    IsTextPopupOpen = false;
                }
                OnPropertyChanged();
            }
        }
        private string _currentPenType = "Brush";
        public string CurrentPenType
        {
            get => _currentPenType;
            set { _currentPenType = value; OnPropertyChanged(); }
        }

        private string _currentColor = "#000000";
        public string CurrentColor
        {
            get => _currentColor;
            set {
                _currentColor = value;
                OnPropertyChanged(); // Thông báo đổi màu
                OnPropertyChanged(nameof(CurrentColorBrush));
            }
        }

        //hàm xử lý khi người dùng chọn một màu mới từ bảng màu
        public void AddCustomColor(string hexColor)
        {
            if (!RecentColors.Contains(hexColor))
            {
                if (RecentColors.Count >= 5)
                {
                    RecentColors.RemoveAt(0); // Xóa màu cũ nhất nếu đã đủ 5 màu
                }
                RecentColors.Add(hexColor);
            }
            ExecuteChangeColor(hexColor);
        }
        // ================= TOOL SELECTION (HIGHLIGHT FIX) =================
        private bool _isPencilSelected = false;
        public bool IsPencilSelected
        {
            get => _isPencilSelected;
            set
            {
                if (_isPencilSelected == value)
                    return;

                _isPencilSelected = value;
                OnPropertyChanged();

                if (value && _isEraserSelected)
                {
                    _isEraserSelected = false;
                    OnPropertyChanged(nameof(IsEraserSelected));
                }

                UpdateCurrentSize();
            }
        }
        private bool _isEraserSelected;
        public bool IsEraserSelected
        {
            get => _isEraserSelected;
            set
            {
                if (_isEraserSelected == value)
                    return;

                _isEraserSelected = value;
                OnPropertyChanged();

                if (value && _isPencilSelected)
                {
                    _isPencilSelected = false;
                    OnPropertyChanged(nameof(IsPencilSelected));
                }

                UpdateCurrentSize();
            }
        }

        private bool _isShapeSelected;
        public bool IsShapeSelected
        {
            get => _isShapeSelected;
            set
            {
                _isShapeSelected = value;
                OnPropertyChanged();
            }
        }

        private bool _isTextSelected;
        public bool IsTextSelected
        {
            get => _isTextSelected;
            set
            {
                _isTextSelected = value;
                OnPropertyChanged();
            }
        }
        // ================= SIZE =================

        private double _pencilSize = 2;
        public double PencilSize
        {
            get => _pencilSize;
            set
            {
                _pencilSize = value;
                OnPropertyChanged();
                UpdateCurrentSize();
            }
        }

        private double _eraserSize = 20;
        public double EraserSize
        {
            get => _eraserSize;
            set
            {
                _eraserSize = value;
                OnPropertyChanged();
                UpdateCurrentSize();
            }
        }

        private double _currentThickness = 1.0;
        public double CurrentThickness
        {
            get => _currentThickness;
            set
            {
                _currentThickness = value;
                OnPropertyChanged();
            }
        }

        private string _currentShapeColor = "#000000";
        public string CurrentShapeColor
        {
            get => _currentShapeColor;
            set
            {
                _currentShapeColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentShapeColorBrush));
            }
        }
        public SolidColorBrush CurrentShapeColorBrush => (SolidColorBrush)new BrushConverter().ConvertFromString(CurrentShapeColor ?? "#000000");
        private double _currentShapeThickness = 1.0;
        public double CurrentShapeThickness
        {
            get => _currentShapeThickness;
            set
            {
                _currentShapeThickness = value;
                OnPropertyChanged();
            }
        }
        // FIX: method không còn tham số (tránh lỗi compile)
        private void UpdateCurrentSize()
        {
            if (IsEraserSelected)
                CurrentThickness = EraserSize;
            else
                CurrentThickness = PencilSize;
        }

        // ================= TOOL TOGGLE =================

        private void TogglePopup(string type)
        {
            switch (type)
            {
                case "Pencil":
                    IsPencilPopupOpen = !IsPencilPopupOpen;
                    break;

                case "Eraser":
                    IsEraserPopupOpen = !IsEraserPopupOpen;
                    break;

                case "Shape":
                    IsShapePopupOpen = !IsShapePopupOpen;
                    break;

                case "Text":
                    IsTextPopupOpen = !IsTextPopupOpen;
                    break;

                case "ORC":
                    IsORCMagicPopupOpen = !IsORCMagicPopupOpen;
                    break;
            }

            ToolSelected?.Invoke(this, type.ToLower());
        }

        public ICommand TogglePopupCommand { get; }
        public ICommand ChangeSizeCommand { get; } // Thêm Command nhận lệnh từ UI
                                                   // THÊM VÀO PHẦN KHAI BÁO COMMAND:
        public ICommand ChangePenTypeCommand { get; }
        public ICommand ChangeColorCommand { get; }
        public ICommand ChangeShapeColorCommand { get; }
        public ICommand ChangeShapeThicknessCommand { get; }


        public SolidColorBrush CurrentColorBrush =>
            (SolidColorBrush)new BrushConverter().ConvertFromString(CurrentColor ?? "#000000");

        public ToolbarViewModel()
        {
            TogglePopupCommand = new RelayCommand<string>(TogglePopup);
            ChangeSizeCommand = new RelayCommand<string>(ExecuteChangeSize);
            ChangePenTypeCommand = new RelayCommand<string>(ExecuteChangePenType);
            ChangeColorCommand = new RelayCommand<string>(ExecuteChangeColor);
            ChangeShapeColorCommand = new RelayCommand<string>(ExecuteChangeShapeColor);
            ChangeShapeThicknessCommand = new RelayCommand<string>(ExecuteChangeShapeThickness);
            // Khởi tạo Command
        }

        // Hàm xử lý đổi size
        private void ExecuteChangeSize(string sizeStr)
        {
            if (double.TryParse(sizeStr, out double size))
            {
                if (IsEraserSelected)
                {
                    EraserSize = size;
                    OnPropertyChanged(nameof(EraserSize)); // Báo thay đổi Eraser
                }
                else
                {
                    PencilSize = size;
                    OnPropertyChanged(nameof(PencilSize)); // Báo thay đổi Bút
                }

                // Gửi sự kiện để Canvas cập nhật ngay lập tức nét vẽ
                //ToolSelected?.Invoke(this, "SizeChanged");
            }
        }
        private void ExecuteChangePenType(string type)
        {
            CurrentPenType = type;
            //ToolSelected?.Invoke(this, "PenTypeChanged");
        }

        public void ExecuteChangeColor(string hexColor)
        {
            CurrentColor = hexColor;
            OnPropertyChanged(nameof(CurrentColorBrush)); // Báo cho giao diện cập nhật màu của cục size
            ToolSelected?.Invoke(this, "ColorChanged");
        }
        public void ExecuteChangeShapeColor(string hexColor)
        {
            CurrentShapeColor = hexColor;
            ToolSelected?.Invoke(this, "shape"); // Đảm bảo tool shape luôn active khi đổi màu
        }
        public void ExecuteChangeShapeThickness(string thicknessStr)
        {
            if (double.TryParse(thicknessStr, out double thickness))
            {
                CurrentShapeThickness = thickness; // Gán vào biến độ dày riêng của Shape
                ToolSelected?.Invoke(this, "shape");
            }
        }
        // ================= INotifyPropertyChanged =================

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ================= RelayCommand =================

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;

        public RelayCommand(Action<T> execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            if (parameter == null && typeof(T).IsValueType)
                return;

            _execute((T)parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}