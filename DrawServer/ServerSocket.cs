using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace DrawServer
{
    public class ServerSocket
    {
        private TcpListener server;

        // roomId -> clients
        private ConcurrentDictionary<string, ConcurrentDictionary<TcpClient, byte>> rooms
            = new ConcurrentDictionary<string, ConcurrentDictionary<TcpClient, byte>>();

        public void Start(int port)
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();

            Console.WriteLine("Server started...");

            new Thread(() =>
            {
                while (true)
                {
                    var client = server.AcceptTcpClient();
                    client.NoDelay = true;

                    Console.WriteLine("Client connected");

                    new Thread(() => HandleClient(client)).Start();
                }
            })
            { IsBackground = true }.Start();
        }

        private void HandleClient(TcpClient client)
        {
            var stream = client.GetStream();
            byte[] buffer = new byte[4096];
            var sb = new StringBuilder();

            try
            {
                while (true)
                {
                    int len = stream.Read(buffer, 0, buffer.Length);
                    if (len <= 0) break;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, len));

                    while (true)
                    {
                        string data = sb.ToString();
                        int idx = data.IndexOf('\n');
                        if (idx < 0) break;

                        string msg = data.Substring(0, idx);
                        sb.Remove(0, idx + 1);

                        HandleMessage(client, msg);
                    }
                }
            }
            catch
            {
                Console.WriteLine("Client lost");
            }
            finally
            {
                RemoveClient(client);
                client.Close();
            }
        }

        private void HandleMessage(TcpClient client, string msg)
        {
            // thử parse DrawMessage
            try
            {
                var draw = JsonSerializer.Deserialize<DrawMessage>(msg);

                if (draw != null && !string.IsNullOrEmpty(draw.type))
                {
                    if (draw.type == "JOIN")
                    {
                        var room = rooms.GetOrAdd(draw.roomId,
                            _ => new ConcurrentDictionary<TcpClient, byte>());

                        room[client] = 0;

                        Console.WriteLine($"Client joined room {draw.roomId}");
                        return;
                    }

                    // broadcast line
                    Broadcast(draw.roomId, msg + "\n", client);
                    return;
                }
            }
            catch { }
            // thử parse DrawEvent
            try
            {
                var drawEvent = JsonSerializer.Deserialize<DrawEvent>(msg);

                if (drawEvent != null && !string.IsNullOrEmpty(drawEvent.type))
                {
                    // join
                    if (drawEvent.type == "JOIN")
                    {
                        var room = rooms.GetOrAdd(drawEvent.roomId,
                            _ => new ConcurrentDictionary<TcpClient, byte>());

                        room[client] = 0;

                        Console.WriteLine($"Client joined room {drawEvent.roomId}");
                        return;
                    }

                    // broadcast brush event
                    Broadcast(drawEvent.roomId, msg + "\n", client);
                    return;
                }
            }
            catch { }
        }

        private void Broadcast(string roomId, string msg, TcpClient sender)
        {
            if (!rooms.ContainsKey(roomId)) return;

            byte[] data = Encoding.UTF8.GetBytes(msg);

            foreach (var client in rooms[roomId].Keys)
            {
                if (client == sender) continue;

                try
                {
                    client.GetStream().Write(data, 0, data.Length);
                }
                catch
                {
                    RemoveClient(client);
                }
            }
        }

        private void RemoveClient(TcpClient client)
        {
            foreach (var room in rooms.Values)
            {
                room.TryRemove(client, out _);
            }
        }
    }
}