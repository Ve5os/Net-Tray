using System;

namespace NetTray.Models
{
    public class NetworkStatusChangedEventArgs : EventArgs
    {
        public bool IsOnline { get; }
        public long? Ping { get; }

        public NetworkStatusChangedEventArgs(bool isOnline, long? ping)
        {
            IsOnline = isOnline;
            Ping = ping;
        }
    }
}