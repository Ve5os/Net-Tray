using NetTray.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace NetTray.Services
{
    public class NetworkMonitor : IDisposable
    {
        private Thread _monitorThread;
        private bool _isRunning;
        private string _targetHost = "www.youtube.com";
        private int _checkInterval = 2000;

        public List<PingResult> PingHistory { get; private set; }
        public event EventHandler<NetworkStatusChangedEventArgs> StatusChanged;

        public NetworkMonitor()
        {
            PingHistory = new List<PingResult>();
            _isRunning = true;

            _monitorThread = new Thread(MonitorLoop)
            {
                IsBackground = true
            };
            _monitorThread.Start();
        }

        private void MonitorLoop()
        {
            while (_isRunning)
            {
                CheckNetworkStatus();
                Thread.Sleep(_checkInterval);
            }
        }

        private void CheckNetworkStatus()
        {
            long? pingResult = null;
            bool isOnline = false;

            try
            {
                // Пробуем ping
                using (var ping = new Ping())
                {
                    var reply = ping.Send(_targetHost, 3000);
                    isOnline = reply.Status == IPStatus.Success;
                    pingResult = isOnline ? (long?)reply.RoundtripTime : null;
                }

                // Если ping не прошел, пробуем HTTP запрос
                if (!isOnline)
                {
                    isOnline = CheckHttpConnection();
                }
            }
            catch
            {
                isOnline = false;
            }

            var result = new PingResult
            {
                Timestamp = DateTime.Now,
                RoundtripTime = pingResult,
                IsSuccess = isOnline
            };

            lock (PingHistory)
            {
                PingHistory.Add(result);
                if (PingHistory.Count > 30)
                    PingHistory.RemoveAt(0);
            }

            StatusChanged?.Invoke(this, new NetworkStatusChangedEventArgs(isOnline, pingResult));
        }

        private bool CheckHttpConnection()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("http://www.youtube.com");
                request.Timeout = 3000;
                request.Method = "HEAD";

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _isRunning = false;
            if (_monitorThread != null && _monitorThread.IsAlive)
            {
                _monitorThread.Join(1000);
            }
        }
    }
}