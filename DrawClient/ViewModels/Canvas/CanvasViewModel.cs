using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Text.Json;
using System.Windows.Media;
using DrawClient.Models;
using System.Security.Policy;
using System.Windows;
using System;

namespace DrawClient.ViewModels
{

    public class UserParticipant
    {
        public string Initials { get; set; }
        public string ColorHex { get; set; }
    }

    public class ChatMessage
    {
        public string User { get; set; }
        public string Message { get; set; }
        public string Time { get; set; }
    }
    public class CanvasViewModel : INotifyPropertyChanged
    {
        private string _roomName;
        public string RoomName
        {
            get => _roomName;
            set { _roomName = value; OnPropertyChanged(); }
        }

        private string _roomId;
        public ICommand LeaveRoomCommand { get; }
        public Action GoBackToLobby { get; set; }

        // Danh sách dữ liệu
        public ObservableCollection<UserParticipant> Users { get; set; }
        public ObservableCollection<string> NetworkLogs { get; set; }
        public ObservableCollection<ChatMessage> ChatMessages { get; set; }

        // Các State quản lý Giao diện


        private bool _isSidebarOpen = true;
        public bool IsSidebarOpen
        {
            get => _isSidebarOpen;
            set { _isSidebarOpen = value; OnPropertyChanged(); }
        }

        private int _playbackProgress = 0;
        public int PlaybackProgress
        {
            get => _playbackProgress;
            set { _playbackProgress = value; OnPropertyChanged(); }
        }

        private string _activeTool = "Pen";
        public string ActiveTool
        {
            get => _activeTool;
            set { _activeTool = value; OnPropertyChanged(); }
        }

        private string _currentColor = "#000000";
        public string CurrentColor
        {
            get => _currentColor;
            set { _currentColor = value; OnPropertyChanged(); }
        }

        private double _currentThickness = 2.0;
        public double CurrentThickness
        {
            get => _currentThickness;
            set { _currentThickness = value; OnPropertyChanged(); }
        }

        public event Action<Point, Point, string, double> OnLineReceived;
        public CanvasViewModel(string roomName, string roomId)
        {
            RoomName = roomName;
            _roomId = roomId;

            LeaveRoomCommand = new RelayCommand(ExecuteLeaveRoom);

            // Mock Data
            Users = new ObservableCollection<UserParticipant>
            {
                new UserParticipant { Initials = "SC", ColorHex = "#1A73E8" },
                new UserParticipant { Initials = "MJ", ColorHex = "#34A853" },
                new UserParticipant { Initials = "ED", ColorHex = "#FBBC04" }
            };

            NetworkLogs = new ObservableCollection<string>
            {
                "14:23:45 → Encrypted packet sent",
                "14:23:46 ← Sync received from client 2",
                "14:23:48 ← User 'Mike' joined"
            };

            ChatMessages = new ObservableCollection<ChatMessage>
            {
                new ChatMessage { User = "Sarah Chen", Message = "alooo", Time = "14:20" },
                new ChatMessage { User = "Mike Johnson", Message = "aaalo", Time = "14:21" }
            };
        }

        // Logic gửi dữ liệu lên mạng

        public void SendDrawData(Point p1, Point p2)
        {
            var msg = new DrawMessage
            {
                type = "DRAW",
                roomId = "room1", // Tạm fix cứng, sau này lấy từ thông tin phòng
                x1 = p1.X,
                y1 = p1.Y,
                x2 = p2.X,
                y2 = p2.Y,
                color = this.CurrentColor,
                thickness = this.CurrentThickness
            };

            ClientSocket.Instance.Send(msg);
        }

        private void ExecuteLeaveRoom(object obj)
        {
            var leaveMsg = new DrawMessage { type = "LEAVE", roomId = _roomId };
            ClientSocket.Instance.Send(leaveMsg);
            GoBackToLobby?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}