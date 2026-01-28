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
                // 1. ПРОБУЕМ HTTPS СРАЗУ (главный критерий)
                isOnline = CheckHttpConnection();

                // 2. ЕСЛИ HTTPS РАБОТАЕТ - измеряем пинг для информации
                if (isOnline)
                {
                    using (var ping = new Ping())
                    {
                        var reply = ping.Send(_targetHost, 2000); // Быстрый таймаут для пинга
                        if (reply.Status == IPStatus.Success)
                        {
                            pingResult = reply.RoundtripTime;
                        }
                        // Если пинг не работает, но HTTPS работает - всё равно онлайн
                        // Просто пинг = null
                    }
                }
                // Если HTTPS не работает - уже офлайн, ping не измеряем
            }
            catch
            {
                isOnline = false;
                pingResult = null;
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
                if (PingHistory.Count > 30) PingHistory.RemoveAt(0);
            }

            StatusChanged?.Invoke(this, new NetworkStatusChangedEventArgs(isOnline, pingResult));
        }

        private bool CheckHttpConnection()
        {
            try
            {
                // Проверяем именно доступность YouTube
                var request = (HttpWebRequest)WebRequest.Create("https://www.youtube.com");
                request.Timeout = 4000; // 4 секунды на HTTPS
                request.Method = "HEAD";
                request.UserAgent = "Mozilla/5.0";

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    // YouTube доступен если код ответа 2xx или 3xx
                    int code = (int)response.StatusCode;
                    return code >= 200 && code < 400;
                }
            }
            catch (WebException ex)
            {
                // Если ConnectFailure, NameResolutionFailure, Timeout - точно офлайн
                if (ex.Status == WebExceptionStatus.ConnectFailure ||
                    ex.Status == WebExceptionStatus.NameResolutionFailure ||
                    ex.Status == WebExceptionStatus.Timeout)
                {
                    return false;
                }

                // Другие ошибки (например, прокси, SSL) - считаем офлайн на всякий случай
                return false;
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