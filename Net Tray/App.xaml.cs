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

        // КЭШ ИКОНОК - создаем один раз
        private Icon _greenIcon;
        private Icon _redIcon;
        private Icon _currentIcon = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // СОЗДАЕМ ИКОНКИ ОДИН РАЗ ПРИ ЗАПУСКЕ
            _greenIcon = CreateIcon(Color.Green);
            _redIcon = CreateIcon(Color.Red);

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

            // Используем красную иконку по умолчанию
            _trayIcon.Icon = _redIcon;
            _currentIcon = _redIcon;

            _trayIcon.Text = "NetTray - Проверка сети";
            _trayIcon.Visible = true;

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Показать график", null, OnShowChartClicked);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Выход", null, OnExitClicked);

            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.MouseClick += TrayIcon_Click;
        }

        // ИСПРАВЛЕННЫЙ МЕТОД СОЗДАНИЯ ИКОНКИ
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

                // ПРОСТОЙ СПОСОБ - создаем иконку напрямую
                IntPtr hIcon = bmp.GetHicon();
                Icon icon = Icon.FromHandle(hIcon);

                // Клонируем, чтобы можно было освободить оригинал
                Icon clonedIcon = (Icon)icon.Clone();
                icon.Dispose(); // Освобождаем оригинальную иконку

                return clonedIcon;
            }
            catch
            {
                // Возвращаем стандартную иконку при ошибке
                return SystemIcons.Application;
            }
            finally
            {
                bmp?.Dispose();
            }
        }

        private void OnNetworkStatusChanged(object sender, Models.NetworkStatusChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Проверяем, какая иконка нужна
                bool needGreenIcon = e.IsOnline;

                if ((needGreenIcon && _currentIcon != _greenIcon) ||
                    (!needGreenIcon && _currentIcon != _redIcon))
                {
                    _trayIcon.Icon = needGreenIcon ? _greenIcon : _redIcon;
                    _currentIcon = needGreenIcon ? _greenIcon : _redIcon;
                }

                _trayIcon.Text = e.IsOnline ?
                    $"NetTray - YouTube доступен ({e.Ping}ms)" :
                    "NetTray - YouTube недоступен";
            });
        }

        private void TrayIcon_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) TogglePopup();
        }

        private void OnShowChartClicked(object sender, EventArgs e) => TogglePopup();

        private void OnExitClicked(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }

        private void TogglePopup()
        {
            if (_popupWindow == null || !_popupWindow.IsVisible)
            {
                _popupWindow = new PopupWindow(_networkMonitor);
                _popupWindow.Closed += (s, args) => _popupWindow = null;
                _popupWindow.ShowWithAnimation();
            }
            else
            {
                _popupWindow.CloseWithAnimation();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // ОСВОБОЖДАЕМ РЕСУРСЫ В ПРАВИЛЬНОМ ПОРЯДКЕ
            _popupWindow?.Close();

            _networkMonitor?.Dispose();
            _networkMonitor = null;

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;

                // Важно: сначала убираем иконку, потом освобождаем
                _trayIcon.Icon = null;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            // Освобождаем кэшированные иконки
            _greenIcon?.Dispose();
            _redIcon?.Dispose();
            _greenIcon = null;
            _redIcon = null;
            _currentIcon = null;

            base.OnExit(e);
        }
    }
}