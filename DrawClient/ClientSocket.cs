using DrawClient.Models;
using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Interop;

namespace DrawClient
{
    public class ClientSocket
    {
        public static ClientSocket Instance { get; } = new ClientSocket();
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;

        public Action<string> OnMessageReceived;
        // Khóa đồng bộ tránh lỗi xung đột luồng khi xử lý chuỗi gói tin
        private readonly object _bufferLock = new object();
        private StringBuilder buffer = new StringBuilder();
        private string currentRoomId;

        public int CurrentUserId { get; set; }
        public string CurrentUsername { get; set; }

        private bool _isRunning = false;

        public bool ConnectAndJoinRoomViaMaster(string masterIp, int masterPort, string roomId)
        {
            try
            {
                Console.WriteLine($"[CLIENT] Đang kết nối Master Server {masterIp}:{masterPort}...");

                using (TcpClient masterClient = new TcpClient(masterIp, masterPort))
                {
                    var masterStream = masterClient.GetStream();

                    var req = new MasterRequest
                    {
                        type = "JOIN_ROOM",
                        roomId = roomId
                    };

                    string reqJson = JsonSerializer.Serialize(req) + "\n";
                    byte[] reqData = Encoding.UTF8.GetBytes(reqJson);

                    masterStream.Write(reqData, 0, reqData.Length);

                    byte[] resBuffer = new byte[1024];
                    int bytesRead = masterStream.Read(resBuffer, 0, resBuffer.Length);

                    if (bytesRead > 0)
                    {
                        string resStr = Encoding.UTF8.GetString(resBuffer, 0, bytesRead).Trim();
                        var res = JsonSerializer.Deserialize<MasterResponse>(resStr);

                        if (res != null && res.success)
                        {
                            return ConnectToNodeAndJoin(res.nodeIp, res.nodePort, roomId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CONNECT MASTER ERROR: " + ex.Message);
            }

            return false;
        }

        private bool ConnectToNodeAndJoin(string nodeIp, int nodePort, string roomId)
        {
            try
            {
                if (client != null && client.Connected)
                {
                    stream?.Close();
                    client.Close();
                }

                client = new TcpClient();
                client.NoDelay = true;
                client.Connect(nodeIp, nodePort);

                stream = client.GetStream();
                // Reset bộ đệm chuỗi sạch sẽ trước khi nhận dữ liệu phòng mới
                lock (_bufferLock)
                {
                    buffer.Clear();
                }

                _isRunning = true;

                receiveThread = new Thread(ReceiveLoop);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                currentRoomId = roomId;

                Send(new DrawMessage
                {
                    type = "JOIN",
                    roomId = currentRoomId,
                    userId = CurrentUserId,
                    username = CurrentUsername
                });

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("CONNECT NODE ERROR: " + ex.Message);
                return false;
            }
        }

        public bool Connect(string ip, int port)
        {
            try
            {
                client = new TcpClient();
                client.NoDelay = true;
                client.Connect(ip, port);

                stream = client.GetStream();

                _isRunning = true;

                receiveThread = new Thread(ReceiveLoop);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("CONNECT ERROR: " + ex.Message);
                return false;
            }
        }

        public void JoinRoom(string roomId)
        {
            if (client == null || !client.Connected) return;

            currentRoomId = roomId;

            Send(new DrawMessage
            {
                type = "JOIN",
                roomId = currentRoomId,
                userId = CurrentUserId,
                username = CurrentUsername
            });
        }
        // Hàm chủ động rời phòng cho Client bấm nút Thoát
        public void LeaveRoom()
        {
            if (client == null || !client.Connected || string.IsNullOrEmpty(currentRoomId)) return;

            // Gửi gói tin LEAVE báo cho Server biết tôi chủ động thoát
            Send(new DrawMessage
            {
                type = "LEAVE",
                roomId = currentRoomId,
                userId = CurrentUserId,
                username = CurrentUsername
            });

            currentRoomId = null;
            Disconnect(); // Sau khi báo Server thì tự ngắt kết nối socket luôn
        }

        #region RECEIVE
        private void ReceiveLoop()
        {
            byte[] receiveBuffer = new byte[4096];

            try
            {
                while (_isRunning)
                {
                    if (stream == null || !client.Connected)
                        break;

                    int len = stream.Read(receiveBuffer, 0, receiveBuffer.Length);
                    if (len <= 0) break;

                    lock (_bufferLock)
                    {
                        buffer.Append(Encoding.UTF8.GetString(receiveBuffer, 0, len));

                        while (true)
                        {
                            string content = buffer.ToString();
                            int index = content.IndexOf('\n');
                            if (index < 0) break;

                            string msg = content.Substring(0, index);
                            buffer.Remove(0, index + 1);

                            // Đẩy dữ liệu ra UI an toàn
                            if (!string.IsNullOrWhiteSpace(msg))
                            {
                                HandleMessage(msg);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("RECEIVE ERROR: " + ex.Message);
            }
            finally
            {
                Disconnect();
            }
        }
        #endregion

        #region SEND
        public void Send(object obj)
        {
            try
            {
                if (stream == null || client == null || !client.Connected)
                    return;

                string json = JsonSerializer.Serialize(obj) + "\n";

                byte[] data = Encoding.UTF8.GetBytes(json);

                // Gửi dữ liệu bất tuần tự an toàn
                lock (stream)
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SEND ERROR: " + ex.Message);
            }
        }
        #endregion

        #region HANDLE
        private void HandleMessage(string msg)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(msg))
                {
                    if (!doc.RootElement.TryGetProperty("type", out JsonElement typeElement))
                        return;

                    string type = typeElement.GetString();

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    

                    if (
                        type == "HISTORY" || 
                        type == "CHAT_HISTORY"||
                        type == "JOIN" ||
                        type == "DRAW" ||
                        type == "ERASE" ||
                        type == "SHAPE" ||
                        type == "TEXT" ||
                        type == "CLEAR" ||
                        type == "CHAT" ||
                        type == "DELETE_TEXT" ||
                        type == "LEAVE"
                    )
                    {
                        OnMessageReceived?.Invoke(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("HANDLE MESSAGE ERROR: " + ex.Message);
            }
        }
        #endregion

        public void Disconnect()
        {
            _isRunning = false;

            try
            {
                stream?.Close();
            }
            catch { }

            try
            {
                client?.Close();
            }
            catch { }

            stream = null;
            client = null;
            receiveThread = null;
        }
    }
}