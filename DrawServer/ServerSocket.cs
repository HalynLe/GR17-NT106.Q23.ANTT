using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DrawServer
{
    public class ServerSocket
    {
        private string connectionString =
            "server=localhost;database=online_Drawing_DB;user=root;password=";

        private TcpListener server;

        // Quản lý phòng: roomId -> danh sách các Client trong phòng đó
        private ConcurrentDictionary<string, ConcurrentDictionary<TcpClient, byte>> rooms
            = new ConcurrentDictionary<string, ConcurrentDictionary<TcpClient, byte>>();

        // Quản lý thông tin User trên mỗi Connection (UserId, RoomId, Username)
        private ConcurrentDictionary<TcpClient, (int UserId, string RoomId, string Username)> clientMetadata
            = new ConcurrentDictionary<TcpClient, (int UserId, string RoomId, string Username)>();
        private bool _isRunning = true;
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string MasterApiUrl = "http://localhost:5274/api/room/update-status";

        public void Start(int port)
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            Console.WriteLine($"[NODE SERVER] Đang chạy tại cổng: {port}...");

            Task.Run(() => AcceptClientsAsync());
        }

        private async Task AcceptClientsAsync()
        {
            while (true)
            {
                try
                {
                    // Lắng nghe bất đồng bộ, giải phóng CPU khi không có ai kết nối
                    var client = await server.AcceptTcpClientAsync();
                    client.NoDelay = true;
                    Console.WriteLine("Có một Client mới kết nối vào Node.");

                    // Giao Client này cho 1 Task độc lập xử lý
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception ex) { Console.WriteLine("Lỗi Accept: " + ex.Message); }
            }
        }

       private async Task HeartbeatCheckAsync(TcpClient client)
        {
            while (_isRunning && client.Connected)
            {
                await Task.Delay(30000);
                try
                {
                    if (!client.Connected) break;
                    var pingMsg = new DrawMessage { type = "PING" };
                    string json = JsonSerializer.Serialize(pingMsg) + "\n";
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    await client.GetStream().WriteAsync(data, 0, data.Length);
                }
                catch { break; }
            }
        }

        // Trong ServerSocket.cs - Phương thức HandleClient
        private async Task HandleClientAsync(TcpClient client)
        {
            using (NetworkStream stream = client.GetStream())
            {
                byte[] bytes = new byte[8192]; // Buffer lớn hơn một chút
                StringBuilder messageBuffer = new StringBuilder();
                 _ = HeartbeatCheckAsync(client);

                try
                {
                    int i;
                    while ((i = await stream.ReadAsync(bytes, 0, bytes.Length)) != 0)
                    {
                        string data = Encoding.UTF8.GetString(bytes, 0, i);
                        messageBuffer.Append(data);

                        // Xử lý tất cả các tin nhắn hoàn chỉnh trong buffer (kết thúc bằng \n)
                        string currentContent = messageBuffer.ToString();
                        int nextLineIndex;

                        while ((nextLineIndex = currentContent.IndexOf('\n')) != -1)
                        {
                            string singleMessage = currentContent.Substring(0, nextLineIndex).Trim();
                            if (!string.IsNullOrEmpty(singleMessage))
                            {
                                ProcessLogic(client, singleMessage); // Tách logic xử lý ra hàm riêng
                            }

                            currentContent = currentContent.Substring(nextLineIndex + 1);
                            messageBuffer.Clear();
                            messageBuffer.Append(currentContent);
                        }
                    }
                }

                catch (Exception ex)
                {
                    Console.WriteLine($"[NODE SERVER] Client ngắt kết nối: {ex.Message}");
                }
                finally
                {
                    string targetRoomId = null;
                    int targetUserId = 0;
                    string targetUsername = null;
                    if (clientMetadata.TryRemove(client, out var metadata))
                    {
                        targetRoomId = metadata.RoomId;
                        targetUserId = metadata.UserId;
                        targetUsername = metadata.Username;
                        
                        // Cập nhật is_online = 0 trong DB
                        using (var conn = new MySqlConnection(connectionString))
                        {
                            conn.Open();
                            string updateSql = "UPDATE RoomMembers SET is_online = 0 WHERE user_id = @uid AND room_id = @rid";
                            using (var cmd = new MySqlCommand(updateSql, conn))   // <--- ĐÃ SỬA
                            {
                                cmd.Parameters.AddWithValue("@uid", targetUserId);
                                cmd.Parameters.AddWithValue("@rid", int.Parse(targetRoomId));
                                cmd.ExecuteNonQuery();
                            }
                        }

                        _ = NotifyMasterStatusChanged(targetUserId, int.Parse(targetRoomId), false);
                    }

                    // 2. Xóa client khỏi danh sách phòng của Server trước 
                    // (Để khi broadcast, Server không gửi ngược lại chính socket đã chết này)
                    RemoveClientFromAllRooms(client);
                    client.Close();

                    // 3. Phát tín hiệu LEAVE cho những người còn lại trong phòng
                    if (targetRoomId != null)
                    {
                        var leaveMsg = new DrawMessage
                        {
                            type = "LEAVE",
                            roomId = targetRoomId,
                            userId = targetUserId,
                            username = targetUsername // Frontend có thể dựa vào userId để xóa cursor/hiển thị thông báo
                        };

                        string leaveJson = JsonSerializer.Serialize(leaveMsg);
                        BroadcastToRoom(targetRoomId, leaveJson, client);
                    }

                    Console.WriteLine($"[NODE SERVER] Client {targetUserId} disconnected + cleaned up room {targetRoomId}");
                }
            }
        }
        private void ProcessLogic(TcpClient client, string jsonMsg)
        {
            try
            {
                Console.WriteLine("RAW JSON = " + jsonMsg);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var msg = JsonSerializer.Deserialize<DrawMessage>(jsonMsg, options);
                if (msg == null) return;

                if (msg.type == "JOIN")
                {
                    // Phân loại phòng: Lấy danh sách client của phòng này, hoặc tạo mới nếu phòng chưa tồn tại
                    var room = rooms.GetOrAdd(msg.roomId, _ => new ConcurrentDictionary<TcpClient, byte>());
                    // Gửi thông tin của những người ĐANG Ở SẴN trong phòng cho thành viên MỚI VÀO
                    foreach (var existingClient in room.Keys)
                    {
                        if (clientMetadata.TryGetValue(existingClient, out var meta))
                        {
                            var existingUserMsg = new DrawMessage
                            {
                                type = "JOIN",
                                roomId = msg.roomId,
                                userId = meta.UserId,
                                username = meta.Username
                            };
                            string existingJson = JsonSerializer.Serialize(existingUserMsg) + "\n";
                            byte[] existingData = Encoding.UTF8.GetBytes(existingJson);
                            try { client.GetStream().Write(existingData, 0, existingData.Length); } catch { }
                        }
                    }

                    room[client] = 0; // Thêm client vào phòng
                    // Lưu lại Metadata để xử lý khi thoát
                    // Lưu ý: Client cần gửi kèm userId trong gói tin JOIN
                    clientMetadata[client] = (msg.userId, msg.roomId, msg.username);
                   using (var conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                        string updateSql = @"
                            INSERT INTO RoomMembers (user_id, room_id, is_online, role) 
                            VALUES (@uid, @rid, 1, 'MEMBER')
                            ON DUPLICATE KEY UPDATE is_online = 1";
                        using (var cmd = new MySqlCommand(updateSql, conn))   // <--- ĐÃ SỬA
                        {
                            cmd.Parameters.AddWithValue("@uid", msg.userId);
                            cmd.Parameters.AddWithValue("@rid", int.Parse(msg.roomId));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    // Cập nhật Online trong DB
                    _ = NotifyMasterStatusChanged(msg.userId, int.Parse(msg.roomId), true);

                    Console.WriteLine($"Client {msg.userId} vào phòng: {msg.roomId}");
                    // Phát lệnh JOIN của thành viên mới này cho TẤT CẢ mọi người trong phòng biết để cập nhật UI
                    string joinJson = JsonSerializer.Serialize(msg);
                    BroadcastToRoom(msg.roomId, joinJson, client);
                    // Gửi lịch sử dữ liệu
                    SendHistoryToClient(client, msg.roomId);
                    SendChatHistoryToClient(client, msg.roomId);
                }
                else if (
<<<<<<< HEAD
                    msg.type == "DRAW" ||
                    msg.type == "ERASE" ||
                    msg.type == "SHAPE" ||
                    msg.type == "TEXT" ||
                    msg.type == "CLEAR" ||
                    msg.type == "CHAT"
=======
                     msg.type == "DRAW" ||
                     msg.type == "ERASE" ||
                     msg.type == "SHAPE" ||
                     msg.type == "TEXT" ||
                     msg.type == "CLEAR" ||
                     msg.type == "DELETE_TEXT" ||
                     msg.type == "CHAT"
>>>>>>> fab2d1b366c8423b7efe0aaf700a8f4125580c9d
                )
                {
                    if (clientMetadata.TryGetValue(client, out var metadata))
                    {
                        msg.userId = metadata.UserId;
                    }

                    Console.WriteLine(
                        "[SERVER] ACTION USER ID = "
                        + msg.userId);

                    string updatedJson = JsonSerializer.Serialize(msg);

                    BroadcastToRoom(msg.roomId, updatedJson, client);

                    if (msg.type == "CHAT")
                    {
                        SaveChatMessage(msg);
                    }
                    else
                    {
                        SaveDrawAction(msg);
                    }

                }

                else if (msg.type == "DRAW_BATCH" && msg.actions != null && msg.actions.Count > 0)
                {
                    Console.WriteLine($"[BATCH] Received batch of {msg.actions.Count} actions");
                    foreach (var action in msg.actions)
                    {
                        // Gán lại roomId và userId cho từng action
                        action.roomId = msg.roomId;
                        if (clientMetadata.TryGetValue(client, out var meta))
                            action.userId = meta.UserId;
                        
                        string actionJson = JsonSerializer.Serialize(action);
                        BroadcastToRoom(msg.roomId, actionJson, client);
                        SaveDrawAction(action);
                    }
                }

                else if (msg.type == "UNDO")
                {
                    Console.WriteLine("[SERVER] UNDO command received from user " + msg.userId);
                    
                    // Broadcast UNDO command để tất cả client cùng undo
                    string undoJson = JsonSerializer.Serialize(msg);
                    BroadcastToRoom(msg.roomId, undoJson, client);
                }
                else if (msg.type == "REDO")
                {
                    Console.WriteLine("[SERVER] REDO command received from user " + msg.userId);
                    
                    // Broadcast REDO command để tất cả client cùng redo
                    string redoJson = JsonSerializer.Serialize(msg);
                    BroadcastToRoom(msg.roomId, redoJson, client);
                }
                else if (msg.type == "LEAVE")
                {
                    // Xử lý cập nhật DB khi nhận lệnh LEAVE chủ động
                    if (clientMetadata.TryRemove(client, out var metadata))
                    {
                        _ = NotifyMasterStatusChanged(metadata.UserId, int.Parse(metadata.RoomId), false);
                    }
                    RemoveClientFromRoom(msg.roomId, client);
                    using (var conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                        string updateSql = "UPDATE RoomMembers SET is_online = 0 WHERE user_id = @uid AND room_id = @rid";
                        using (var cmd = new MySqlCommand(updateSql, conn))   // <--- ĐÃ SỬA
                        {
                            cmd.Parameters.AddWithValue("@uid", msg.userId);
                            cmd.Parameters.AddWithValue("@rid", int.Parse(msg.roomId));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    // Gửi thông tin LEAVE này cho tất cả những người còn lại trong phòng
                    string leaveJson = JsonSerializer.Serialize(msg);
                    BroadcastToRoom(msg.roomId, leaveJson, client);

                    Console.WriteLine($"Client {msg.userId} chủ động rời phòng: {msg.roomId}");
                }
            }
            catch (Exception ex) { Console.WriteLine("Lỗi xử lý JSON: " + ex.Message); }
        }

        // Hàm gọi API báo cho Master Server cập nhật Database
        private async Task NotifyMasterStatusChanged(int userId, int roomId, bool isOnline)
        {
            try
            {
                var statusData = new { user_id = userId, room_id = roomId, is_online = isOnline ? 1 : 0 };
                var content = new StringContent(JsonSerializer.Serialize(statusData), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(MasterApiUrl, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Không thể báo cáo trạng thái tới Master: {ex.Message}");
            }
        }

        // DrawServer/ServerSocket.cs - Cải tiến BroadcastToRoom
        private void BroadcastToRoom(string roomId, string rawJson, TcpClient sender)
        {
            if (!rooms.TryGetValue(roomId, out var clients)) return;

            if (!rawJson.EndsWith("\n"))
                rawJson += "\n";

            byte[] data = Encoding.UTF8.GetBytes(rawJson);

            // Gửi parallel để không block thread
            var tasks = new List<Task>();
            
            foreach (var client in clients.Keys)
            {
                if (!client.Connected)
                {
                    clients.TryRemove(client, out _);
                    continue;
                }

                // Gửi không chờ (fire and forget)
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var stream = client.GetStream();
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                    }
                    catch
                    {
                        clients.TryRemove(client, out _);
                    }
                }));
            }
            
            // Không chờ tất cả xong, tiếp tục xử lý client khác
            Task.WaitAll(tasks.ToArray(), TimeSpan.FromMilliseconds(100));
        }
        private void RemoveClientFromRoom(string roomId, TcpClient client)
        {
            if (rooms.TryGetValue(roomId, out var clients))
            {
                clients.TryRemove(client, out _);
            }
        }

        private void SaveDrawAction(DrawMessage msg)
        {
            Console.WriteLine("===== SAVE DRAW =====");
            Console.WriteLine("roomId = " + msg.roomId);
            Console.WriteLine("userId = " + msg.userId);
            Console.WriteLine("type = " + msg.type);

            try
            {
                using (MySqlConnection conn =
                    new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
        INSERT INTO DrawActions
        (user_id, room_id, type, data)
        VALUES
        (@user_id, @room_id, @type, @data)";

                    using (MySqlCommand cmd =
                        new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue(
                            "@user_id",
                            msg.userId);

                        cmd.Parameters.AddWithValue(
                            "@room_id",
                            int.Parse(msg.roomId));

                        cmd.Parameters.AddWithValue(
                            "@type",
                            msg.type);

                        string jsonData =
                            JsonSerializer.Serialize(msg);

                        cmd.Parameters.AddWithValue(
                            "@data",
                            jsonData);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void SaveChatMessage(DrawMessage draw)
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                        INSERT INTO Messages(user_id, room_id, content)
                        VALUES(@uid, @rid, @msg)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", draw.userId);
                        cmd.Parameters.AddWithValue("@rid", draw.roomId);
                        cmd.Parameters.AddWithValue("@msg", draw.text);

                        cmd.ExecuteNonQuery();
                    }
                }

                Console.WriteLine("[CHAT SAVED]");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SAVE CHAT ERROR] " + ex.Message);
            }
        }

        private List<DrawMessage> LoadHistory(string roomId)
        {
            List<DrawMessage> history =
                new List<DrawMessage>();

            try
            {
                using (MySqlConnection conn =
                    new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
        SELECT data
        FROM DrawActions
        WHERE room_id = @room_id
        ORDER BY created_at ASC";

                    using (MySqlCommand cmd =
                        new MySqlCommand(sql, conn))
                    {
                        int roomIdInt = int.Parse(roomId);

                        cmd.Parameters.AddWithValue(
                            "@room_id",
                            roomIdInt);

                        using (var reader =
                            cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string json =
                                    reader.GetString("data");

                                var draw =
                                    JsonSerializer.Deserialize<DrawMessage>(json);

                                if (draw != null)
                                {
                                    history.Add(draw);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[LOAD HISTORY ERROR] " + ex.Message);
            }

            return history;
        }

        private void SendHistoryToClient(TcpClient client, string roomId)
        {
            try
            {
                var history = LoadHistory(roomId);

                var packet = new
                {
                    type = "HISTORY",
                    roomId = roomId,
                    actions = history
                };

                string json =
                    JsonSerializer.Serialize(packet)
                    + "\n";

                byte[] data =
                    Encoding.UTF8.GetBytes(json);

                client.GetStream()
                    .Write(data, 0, data.Length);

                Console.WriteLine(
                    $"Đã gửi {history.Count} history actions");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[SEND HISTORY ERROR] " + ex.Message);
            }
        }

        private List<DrawMessage> LoadChatHistory(string roomId)
        {
            List<DrawMessage> history = new List<DrawMessage>();

            try
            {
                using (MySqlConnection conn =
                    new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                SELECT m.content, m.created_at, u.username, m.user_id
                FROM Messages m
                JOIN Users u ON m.user_id = u.user_id
                WHERE m.room_id = @room_id
                ORDER BY m.created_at ASC";

                    using (MySqlCommand cmd =
                        new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue(
                            "@room_id",
                            int.Parse(roomId));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                history.Add(new DrawMessage
                                {
                                    type = "CHAT",
                                    roomId = roomId,
                                    userId = reader.GetInt32("user_id"),
                                    username = reader.GetString("username"),
                                    text = reader.GetString("content"),
                                    timestamp = reader.GetDateTime("created_at")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[LOAD CHAT HISTORY ERROR] " + ex.Message);
            }

            return history;
        }

        private void SendChatHistoryToClient(TcpClient client, string roomId)
        {
            try
            {
                var history = LoadChatHistory(roomId);

                var packet = new
                {
                    type = "CHAT_HISTORY",
                    roomId = roomId,
                    messages = history
                };

                string json =
                    JsonSerializer.Serialize(packet) + "\n";

                byte[] data =
                    Encoding.UTF8.GetBytes(json);

                client.GetStream().Write(data, 0, data.Length);

                Console.WriteLine(
                    $"Đã gửi {history.Count} chat messages");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[SEND CHAT HISTORY ERROR] " + ex.Message);
            }
        }

        private void RemoveClientFromAllRooms(TcpClient client)
        {
            foreach (var room in rooms.Values)
            {
                room.TryRemove(client, out _);
            }
        }
    }
}