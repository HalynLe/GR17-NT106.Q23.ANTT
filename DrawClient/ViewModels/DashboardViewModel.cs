
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Windows;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DrawClient.ViewModels
{
    public class Room
    {
        public int room_id { get; set; }
        public string room_name { get; set; }
        public int max_users { get; set; }
        public bool is_private { get; set; }
        public int player_count { get; set; }
        public NodeInfo node { get; set; }

        public string Id => room_id.ToString();
        public string Name => room_name;
        public string Host => "Admin";

        public int PlayerCount => player_count;

        public string HostAvatar => "R";
        public string PlayerCountText => $"{PlayerCount} players";
    }

    public class NodeInfo
    {
        public string ip { get; set; }
        public int port { get; set; }
    }
    public class RoomConnectionResponse
    {
        public Room RoomInfo { get; set; }
        public string NodeIp { get; set; }
        public int NodePort { get; set; }
    }
    public class LobbyViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Room> _rooms;

        public ObservableCollection<Room> Rooms
        {
            get => _rooms;
            set
            {
                _rooms = value;
                OnPropertyChanged();
            }
        }

        // FIX: dùng static HttpClient
        private static readonly HttpClient _httpClient = new HttpClient();

        // TODO:
        // đổi localhost thành IP server thật nếu chạy nhiều máy
        private const string BaseUrl = "http://localhost:5274/api/room";

        private string _newRoomName = "My Awesome Room";

        public string NewRoomName
        {
            get => _newRoomName;
            set
            {
                _newRoomName = value;
                OnPropertyChanged();
            }
        }

        private string _newRoomPassword = "";

        public string NewRoomPassword
        {
            get => _newRoomPassword;
            set
            {
                _newRoomPassword = value;
                OnPropertyChanged();
            }
        }

        private bool _newRoomIsPrivate = false;

        public bool NewRoomIsPrivate
        {
            get => _newRoomIsPrivate;
            set
            {
                _newRoomIsPrivate = value;
                OnPropertyChanged();
            }
        }

        private string _joinRoomIdStr = "";

        public string JoinRoomIdStr
        {
            get => _joinRoomIdStr;
            set
            {
                _joinRoomIdStr = value;
                OnPropertyChanged();
            }
        }

        private string _joinRoomPassword = "";

        public string JoinRoomPassword
        {
            get => _joinRoomPassword;
            set
            {
                _joinRoomPassword = value;
                OnPropertyChanged();
            }
        }

        public ICommand JoinRoomCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand JoinManualCommand { get; }
        public ICommand CreateRoomCommand { get; }
        public ICommand RefreshCommand { get; }

        private bool _isProfilePopoverVisible;

        public bool IsProfilePopoverVisible
        {
            get => _isProfilePopoverVisible;
            set
            {
                _isProfilePopoverVisible = value;
                OnPropertyChanged();
            }
        }

        public Action<string, string, string> GoToCanvas { get; set; }

        public LobbyViewModel()
        {
            Rooms = new ObservableCollection<Room>();

            JoinRoomCommand =
                new RelayCommand(ExecuteJoinRoomList);

            JoinManualCommand =
                new RelayCommand(ExecuteJoinRoomManual);

            CreateRoomCommand =
                new RelayCommand(async (obj) =>
                    await ExecuteCreateRoom());

            RefreshCommand =
                new RelayCommand(async (obj) =>
                    await LoadRooms());

            LogoutCommand =
                new RelayCommand(ExecuteLogout);

            _ = LoadRooms();
        }

        private void SetAuthHeader()
        {
            if (!string.IsNullOrWhiteSpace(LoginViewModel.Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        LoginViewModel.Token);
            }
        }

        public async Task LoadRooms()
        {
            try
            {
                SetAuthHeader();

                var response =
                    await _httpClient.GetAsync($"{BaseUrl}/list");

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse =
                        await response.Content.ReadAsStringAsync();

                    var options =
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                    var rooms =
                        JsonSerializer.Deserialize<List<Room>>(
                            jsonResponse,
                            options);

                    if (rooms != null)
                    {
                        Rooms =
                            new ObservableCollection<Room>(rooms);

                        ActiveRoomsCount = Rooms.Count;

                        TotalPlayers =
                            Rooms.Sum(r => r.PlayerCount);
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Không thể tải danh sách phòng.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không thể lấy danh sách phòng: "
                    + ex.Message);
            }
        }

        private async Task ExecuteCreateRoom()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(NewRoomName))
                {
                    MessageBox.Show(
                        "Tên phòng không được để trống.");
                    return;
                }

                var newRoomReq = new
                {
                    room_name = NewRoomName,
                    is_private = NewRoomIsPrivate,
                    password =
                        string.IsNullOrWhiteSpace(NewRoomPassword)
                            ? null
                            : NewRoomPassword,
                    node_id = 1,
                    max_users = 10
                };

                string jsonString =
                    JsonSerializer.Serialize(newRoomReq);

                var content =
                    new StringContent(
                        jsonString,
                        Encoding.UTF8,
                        "application/json");

                SetAuthHeader();

                var response =
                    await _httpClient.PostAsync(
                        $"{BaseUrl}/create",
                        content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    int createdRoomId = 0;

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    var result = JsonSerializer.Deserialize<RoomConnectionResponse>(jsonResponse, options);

                    if (result != null && result.RoomInfo != null)
                    {
                        createdRoomId = result.RoomInfo.room_id;
                    }

                    if (createdRoomId > 0)
                    {
                        await CallJoinApi(createdRoomId, newRoomReq.password);
                        await LoadRooms();
                    }
                    else
                    {
                        MessageBox.Show("Tạo phòng thành công nhưng không bóc tách được dữ liệu ID phòng.");
                    }
                }
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    MessageBox.Show("Lỗi từ server: " + err);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi tạo phòng: " + ex.Message);
            }
        }

        private async void ExecuteJoinRoomManual(object obj)
        {
            try
            {
                if (!int.TryParse(
                    JoinRoomIdStr,
                    out int roomId))
                {
                    MessageBox.Show(
                        "Room ID phải là số hợp lệ!");
                    return;
                }

                string pass =
                    string.IsNullOrWhiteSpace(
                        JoinRoomPassword)
                            ? null
                            : JoinRoomPassword;

                await CallJoinApi(roomId, pass);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Join room error: " + ex.Message);
            }
        }

        private async void ExecuteJoinRoomList(object obj)
        {
            try
            {
                if (obj is Room selectedRoom)
                {
                    if (selectedRoom.is_private)
                    {
                        MessageBox.Show(
                            $"Phòng này là Private. " +
                            $"Vui lòng nhập Room ID " +
                            $"({selectedRoom.room_id}) " +
                            $"và mật khẩu.");

                        JoinRoomIdStr =
                            selectedRoom.room_id.ToString();
                    }
                    else
                    {
                        await CallJoinApi(
                            selectedRoom.room_id,
                            null);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Join room error: " + ex.Message);
            }
        }

        private async Task CallJoinApi(
            int roomId,
            string password)
        {
            try
            {
                var joinReq = new
                {
                    room_id = roomId,
                    password = password
                };

                string jsonString =
                    JsonSerializer.Serialize(joinReq);

                var content =
                    new StringContent(
                        jsonString,
                        Encoding.UTF8,
                        "application/json");

                SetAuthHeader();

                var response =
                    await _httpClient.PostAsync(
                        $"{BaseUrl}/join",
                        content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    var result = JsonSerializer.Deserialize<RoomConnectionResponse>(jsonResponse, options);

                    if (result == null || result.RoomInfo == null || string.IsNullOrEmpty(result.NodeIp))
                    {
                        MessageBox.Show("Dữ liệu phân phối phòng từ Master Server không hợp lệ.");
                        return;
                    }

                    ClientSocket.Instance.CurrentUserId = LoginViewModel.CurrentUserId;
                    ClientSocket.Instance.CurrentUsername = LoginViewModel.CurrentUsername;

                    // 1. Kết nối Socket trực tiếp tới địa chỉ IP và Cổng của Node được Master phân phối từ DB
                    bool connected = ClientSocket.Instance.Connect(result.NodeIp, result.NodePort);

                    if (connected)
                    {
                        // 2. Gửi lệnh gửi lệnh chào JOIN qua Socket tới Node Server để đồng bộ phòng
                        ClientSocket.Instance.Send(new
                        {
                            type = "JOIN",
                            roomId = result.RoomInfo.Id,
                            userId = ClientSocket.Instance.CurrentUserId,
                            username = ClientSocket.Instance.CurrentUsername
                        });

                        // 3. Chuyển View màn hình sang Canvas vẽ tranh chung
                        GoToCanvas?.Invoke(result.RoomInfo.Id, result.RoomInfo.room_name, password);
                    }
                    else
                    {
                        MessageBox.Show($"Không thể kết nối Realtime tới Máy chủ vẽ được chỉ định [{result.NodeIp}:{result.NodePort}]. Vui lòng thử lại!");
                    }
                }
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    MessageBox.Show("Không thể tham gia phòng: " + err);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Lỗi hệ thống: " + ex.Message);
            }
        }

        private void ExecuteLogout(object obj)
        {
            try
            {
                ClientSocket.Instance.Disconnect();
            }
            catch
            {
            }

            Application.Current.Shutdown();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(
            [CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(name));
        }

        private int _activeRoomsCount = 0;

        public int ActiveRoomsCount
        {
            get => _activeRoomsCount;
            set
            {
                _activeRoomsCount = value;
                OnPropertyChanged();
            }
        }

        private int _totalPlayers = 0;

        public int TotalPlayers
        {
            get => _totalPlayers;
            set
            {
                _totalPlayers = value;
                OnPropertyChanged();
            }
        }
    }
}

