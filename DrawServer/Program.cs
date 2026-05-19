using System;

namespace DrawServer
{
    class Program
    {
        static void Main(string[] args)
        {
            // Bạn có thể đổi Port ở đây nếu muốn
            int port = 6001;

            string connectionString = "server=127.0.0.1;database=online_Drawing_DB;user=root;password=";
            var cleanupService = new RoomCleanupService(connectionString);
            cleanupService.StartCleanupTimer();
            Console.WriteLine("[CLEANUP SERVICE] Started, will delete rooms inactive > 4 weeks");

            Console.Title = "Node Server - Drawing App";
            Console.WriteLine("=======================================");
            Console.WriteLine($"[NODE SERVER] Dang khoi tao tai Port: {port}");

            ServerSocket server = new ServerSocket();
            server.Start(port);

            Console.WriteLine("=======================================");
            Console.WriteLine("Server dang chay. Bam phim bat ky de dong...");
            Console.ReadKey();
        }
    }
}