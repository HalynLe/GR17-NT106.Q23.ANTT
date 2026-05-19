using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace DrawServer
{
    public class DrawMessage
    {
        // Loại tin nhắn: "JOIN", "DRAW", "LEAVE", "CHAT"
        public string type { get; set; }
        public string roomId { get; set; }

        [JsonPropertyName("userId")]
        public int userId { get; set; } // ID của người dùng để Node Server xử lý

        // Dữ liệu vẽ
        public double x1 { get; set; }
        public double y1 { get; set; }
        public double x2 { get; set; }
        public double y2 { get; set; }
        public string color { get; set; }
        public double thickness { get; set; }
        public string shapeType { get; set; }

        public string text { get; set; }

        public double fontSize { get; set; }

        // Dữ liệu chat
        public string username { get; set; }
        public string content { get; set; }
        public DateTime timestamp { get; set; }
        public string actionToUndoId { get; set; } // ID của action bị undo
        public int undoCount { get; set; } // Số lần undo
        public List<DrawMessage> actions { get; set; }
    }
}