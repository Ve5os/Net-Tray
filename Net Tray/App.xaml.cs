using NetTray.Services;
using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;

namespace NetTray
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private PopupWindow _popupWindow = null;
        private NetworkMonitor _networkMonitor;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _networkMonitor = new NetworkMonitor();
            _networkMonitor.StatusChanged += OnNetworkStatusChanged;

            CreateTrayIcon();

            MainWindow = new MainWindow();
            MainWindow.Hide();

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        private void CreateTrayIcon()
        {
            _trayIcon = new NotifyIcon();

            _trayIcon.Icon = CreateIcon(Color.Red);
            _trayIcon.Text = "NetTray - Проверка сети";
            _trayIcon.Visible = true;

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Показать график", null, OnShowChartClicked);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Выход", null, OnExitClicked);

            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.MouseClick += TrayIcon_Click;
        }

        private Icon CreateIcon(Color color)
        {
            Bitmap bmp = null;
            try
            {
                bmp = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    using (SolidBrush brush = new SolidBrush(color))
                    {
                        g.FillEllipse(brush, 0, 0, 15, 15);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
            finally
            {
                bmp?.Dispose(); // ← ГАРАНТИРОВАННОЕ ОСВОБОЖДЕНИЕ
            }
        }

        private void OnNetworkStatusChanged(object sender, Models.NetworkStatusChangedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var oldIcon = _trayIcon.Icon;
                _trayIcon.Icon = CreateIcon(e.IsOnline ? Color.Green : Color.Red);
                oldIcon?.Dispose(); // ← ОСВОБОЖДАЕМ

                // Простой статус в подсказке
                _trayIcon.Text = e.IsOnline ?
                    $"NetTray - Онлайн ({e.Ping}ms)" :
                    "NetTray - Офлайн";
            });
        }

        #region Обработчики событий трея

        private void TrayIcon_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                TogglePopup();
            }
        }

        private void OnShowChartClicked(object sender, EventArgs e)
        {
            TogglePopup();
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _networkMonitor?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        #endregion

        private void TogglePopup()
        {
            if (_popupWindow == null || !_popupWindow.IsVisible)
            {
                // Если окна нет или оно скрыто - создаем и показываем
                _popupWindow = new PopupWindow(_networkMonitor);
                _popupWindow.Closed += (s, args) => _popupWindow = null;
                _popupWindow.ShowWithAnimation();
            }
            else
            {
                // Если окно видимо - закрываем с анимацией (как при клике вне окна)
                _popupWindow.CloseWithAnimation();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon.Icon?.Dispose();
            _trayIcon.Dispose();
            base.OnExit(e);
        }
    }
}