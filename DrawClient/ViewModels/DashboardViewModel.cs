using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace DrawClient.ViewModels
{
    public class Room
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        public int PlayerCount { get; set; }
        public string HostAvatar { get; set; }
        public string PlayerCountText => $"{PlayerCount} {(PlayerCount == 1 ? "player" : "players")}";
    }

    public class LobbyViewModel
    {
        public ObservableCollection<Room> Rooms { get; set; }
        public int TotalPlayers { get; set; }
        public int ActiveRoomsCount { get; set; }

        // Lệnh để chuyển sang màn hình Canvas
        public ICommand JoinRoomCommand { get; }
        public Action<string, string> GoToCanvas { get; set; }

        public LobbyViewModel()
        {
            // Mock data
            Rooms = new ObservableCollection<Room>
            {
                new Room { Id = "1", Name = "Design Sprint Session", Host = "Sarah Chen", PlayerCount = 4, HostAvatar = "SC" },
                new Room { Id = "2", Name = "Team Brainstorm", Host = "Mike Johnson", PlayerCount = 7, HostAvatar = "MJ" },
                new Room { Id = "3", Name = "Product Planning", Host = "Emily Davis", PlayerCount = 3, HostAvatar = "ED" },
                new Room { Id = "4", Name = "UX Workshop", Host = "Alex Kumar", PlayerCount = 5, HostAvatar = "AK" },
                new Room { Id = "5", Name = "Architecture Review", Host = "Chris Lee", PlayerCount = 2, HostAvatar = "CL" },
                new Room { Id = "6", Name = "Quick Sketch", Host = "Jordan Smith", PlayerCount = 1, HostAvatar = "JS" }
            };

            ActiveRoomsCount = Rooms.Count;

            // Tính tổng số người chơi
            foreach (var room in Rooms)
            {
                TotalPlayers += room.PlayerCount;
            }

            JoinRoomCommand = new RelayCommand(ExecuteJoinRoom);
        }

        private void ExecuteJoinRoom(object obj)
        {
            string selectedRoomId = obj as string;

            if (string.IsNullOrEmpty(selectedRoomId))
                return;

            var selectedRoom = Rooms.FirstOrDefault(r => r.Id == selectedRoomId);

            string roomName = selectedRoom != null ? selectedRoom.Name : "Phòng vẽ";

            ClientSocket.Instance.JoinRoom(selectedRoomId);

            GoToCanvas?.Invoke(selectedRoomId, roomName);
        }
    }
}
