using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DrawClient
{
    public class ClientSocket
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;

        public Action<string> OnMessageReceived;

        private StringBuilder buffer = new StringBuilder();

        private string currentRoomId;

        public bool Connect(string ip, int port, string roomId)
        {
            try
            {
                client = new TcpClient();
                client.NoDelay = true;

                client.Connect(ip, port);

                stream = client.GetStream();
                currentRoomId = roomId;

                // JOIN
                Send(new DrawMessage
                {
                    type = "JOIN",
                    roomId = currentRoomId
                });

                receiveThread = new Thread(ReceiveLoop);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                Console.WriteLine("Connected to server");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("CONNECT ERROR: " + ex.Message);
                return false;
            }
        }

        #region RECEIVE
        private void ReceiveLoop()
        {
            byte[] data = new byte[4096];

            try
            {
                while (client.Connected)
                {
                    int len = stream.Read(data, 0, data.Length);
                    if (len <= 0) break;

                    buffer.Append(Encoding.UTF8.GetString(data, 0, len));
                    ProcessBuffer();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("RECEIVE ERROR: " + ex.Message);
            }
        }

        private void ProcessBuffer()
        {
            while (true)
            {
                string content = buffer.ToString();
                int index = content.IndexOf('\n');

                if (index < 0) break;

                string msg = content.Substring(0, index);
                buffer.Remove(0, index + 1);

                HandleMessage(msg);
            }
        }
        #endregion

        #region SEND
        public void Send(object obj)
        {
            try
            {
                if (stream == null || !client.Connected) return;

                string json = JsonSerializer.Serialize(obj);

                byte[] data = Encoding.UTF8.GetBytes(json + "\n");
                stream.Write(data, 0, data.Length);
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
                var draw = JsonSerializer.Deserialize<DrawMessage>(msg);
                if (draw != null && !string.IsNullOrEmpty(draw.type))
                {
                    OnMessageReceived?.Invoke(msg);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Parse DrawMessage error: " + ex.Message);
            }

            try
            {
                var evt = JsonSerializer.Deserialize<DrawEvent>(msg);
                if (evt != null && !string.IsNullOrEmpty(evt.type))
                {
                    OnMessageReceived?.Invoke(msg);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Parse DrawEvent error: " + ex.Message);
            }
        }
        #endregion
    }
}