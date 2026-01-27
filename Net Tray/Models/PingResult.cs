using System;

namespace NetTray.Models
{
    public class PingResult
    {
        public DateTime Timestamp { get; set; }
        public long? RoundtripTime { get; set; }
        public bool IsSuccess { get; set; }
    }
}