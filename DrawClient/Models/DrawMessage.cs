using System;

namespace DrawClient
{
    public class DrawMessage
    {
        public string type { get; set; }
        public string roomId { get; set; }
        public int userId { get; set; }

        public double x1 { get; set; }
        public double y1 { get; set; }
        public double x2 { get; set; }
        public double y2 { get; set; }

        public string color { get; set; }
        public double thickness { get; set; }
        public string penType { get; set; }
        public bool isHighlighter { get; set; }

        // SHAPE
        public string shapeType { get; set; }
        public double width => Math.Abs(x2 - x1);
        public double height => Math.Abs(y2 - y1);
        // TEXT
        public string text { get; set; }
        public double fontSize { get; set; }

        // CHAT
        public string username { get; set; }
        public string content { get; set; }
        public DateTime timestamp { get; set; }
    }
}