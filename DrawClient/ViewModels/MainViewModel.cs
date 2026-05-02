using DrawClient.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DrawClient.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private object _currentView;
        public object CurrentView
        {
            get { return _currentView; }
            set { _currentView = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            // Khi app vừa chạy lên, mở màn hình Login đầu tiên
            //NavigateToLogin();
            NavigateToCanvas("room_test_123", "Canvas test");
        }

        // Các hàm điều hướng
        private void NavigateToLogin()
        {
            var loginVM = new LoginViewModel();
            loginVM.GoToLobby = NavigateToLobby; // Khi đăng nhập xong thì gọi hàm NavigateToLobby
            CurrentView = loginVM;
        }

        private void NavigateToLobby()
        {
            var lobbyVM = new LobbyViewModel();
            lobbyVM.GoToCanvas = NavigateToCanvas; // Khi tạo/vào phòng thì gọi hàm NavigateToCanvas
            CurrentView = lobbyVM;
        }

        private void NavigateToCanvas(string roomId, string roomName)
        {
            var canvasVM = new CanvasViewModel(roomName, roomId);
            canvasVM.GoBackToLobby = NavigateToLobby;
            CurrentView = canvasVM;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}