using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace DrawServer
{
    public class RoomCleanupService
    {
        private readonly string _connectionString;
        private const int CLEANUP_INTERVAL_HOURS = 24;
        private const int INACTIVITY_DAYS = 28;

        public RoomCleanupService(string connStr)
        {
            _connectionString = connStr;
        }

        public void StartCleanupTimer()
        {
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
                while (true)
                {
                    try
                    {
                        await CleanupInactiveRooms();
                        await Task.Delay(TimeSpan.FromHours(CLEANUP_INTERVAL_HOURS));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CLEANUP ERROR] {ex.Message}");
                        await Task.Delay(TimeSpan.FromHours(1));
                    }
                }
            });
        }

        private async Task CleanupInactiveRooms()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string findRoomsSql = @"
                    SELECT r.room_id 
                    FROM Rooms r
                    WHERE NOT EXISTS (
                        SELECT 1 FROM RoomMembers rm 
                        WHERE rm.room_id = r.room_id AND rm.is_online = TRUE
                    )
                    AND (
                        SELECT MAX(created_at) FROM DrawActions WHERE room_id = r.room_id
                    ) < DATE_SUB(NOW(), INTERVAL @days DAY)
                ";

                using (var cmd = new MySqlCommand(findRoomsSql, conn))
                {
                    cmd.Parameters.AddWithValue("@days", INACTIVITY_DAYS);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        // SỬA TẠI ĐÂY: Đổi từ List<string> sang List<int>
                        var roomIds = new List<int>();
                        while (await reader.ReadAsync())
                        {
                            int id = reader.GetInt32(reader.GetOrdinal("room_id"));
                            // SỬA TẠI ĐÂY: Add trực tiếp id kiểu int, không dùng .ToString() nữa
                            roomIds.Add(id);
                        }
                        reader.Close();

                        // SỬA TẠI ĐÂY: Duyệt qua từng int roomId
                        foreach (int roomId in roomIds)
                        {
                            await DeleteRoomData(conn, roomId);
                        }

                        if (roomIds.Count > 0)
                            Console.WriteLine($"[CLEANUP] Deleted {roomIds.Count} inactive rooms (no activity for {INACTIVITY_DAYS} days)");
                    }
                }
            }
        }

        private async Task DeleteRoomData(MySqlConnection conn, int roomId)
        {
            try
            {
                string[] tables = { "RoomMembers", "DrawActions", "Messages", "Rooms" };
                foreach (string table in tables)
                {
                    string sql = $"DELETE FROM {table} WHERE room_id = @room_id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@room_id", roomId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                Console.WriteLine($"[CLEANUP] Room {roomId} deleted");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLEANUP ERROR] Failed to delete room {roomId}: {ex.Message}");
            }
        }
    }
}