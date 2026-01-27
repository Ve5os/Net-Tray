using NetTray.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;

namespace NetTray
{
    public partial class PopupWindow : Window
    {
        private readonly NetworkMonitor _monitor;
        private readonly List<double?> _pingValues = new List<double?>();
        private DispatcherTimer _hideTimer;
        private const int AnimationMS = 100;
        private const int HideDelayMS = 100;
        private bool _isClosing = false;

        // Для отслеживания кликов вне окна
        private static class NativeMethods
        {
            public const int WM_NCLBUTTONDOWN = 0xA1;
            public const int HT_CAPTION = 0x2;

            [DllImport("user32.dll")]
            public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

            [DllImport("user32.dll")]
            public static extern bool ReleaseCapture();
        }

        public PopupWindow(NetworkMonitor monitor)
        {
            InitializeComponent();
            _monitor = monitor;

            InitializeHideTimer();
            DrawGrid();
            InitializeChart();
            SubscribeToEvents();

            // Позволяем перетаскивать окно за любую часть
            this.MouseDown += Window_MouseDown;

            // Перехватываем все клики
            this.PreviewMouseDown += PopupWindow_PreviewMouseDown;
            this.Deactivated += PopupWindow_Deactivated;
            this.LostFocus += PopupWindow_LostFocus;

            // Устанавливаем фокус на окно при открытии
            this.Loaded += (s, e) => this.Focus();
        }

        private void InitializeHideTimer()
        {
            _hideTimer = new DispatcherTimer();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(HideDelayMS);
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                if (!_isClosing)
                {
                    CloseWithAnimation();
                }
            };
        }

        public void ShowWithAnimation()
        {
            // Позиция окна — в правом нижнем углу
            var screen = SystemParameters.WorkArea;

            double targetTop = screen.Bottom - this.Height - 10;
            double startTop = screen.Bottom + 10;

            this.Left = screen.Right - this.Width - 10;
            this.Top = startTop;
            this.Opacity = 0;

            // Показываем окно перед анимацией
            this.Show();
            this.Activate();
            this.Focus();

            // Анимация подъема окна снизу вверх
            var moveAnimation = new DoubleAnimation
            {
                From = startTop,
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(AnimationMS),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // Анимация прозрачности
            var fadeAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(AnimationMS),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(Window.TopProperty, moveAnimation);
            this.BeginAnimation(Window.OpacityProperty, fadeAnimation);
        }

        public void CloseWithAnimation()
        {
            if (_isClosing) return;
            _isClosing = true;

            var screen = SystemParameters.WorkArea;
            double endTop = screen.Bottom + 10;

            // Анимация ухода окна вниз
            var moveAnimation = new DoubleAnimation
            {
                From = this.Top,
                To = endTop,
                Duration = TimeSpan.FromMilliseconds(AnimationMS),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            // Анимация прозрачности
            var fadeAnimation = new DoubleAnimation
            {
                From = this.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(AnimationMS),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            moveAnimation.Completed += (s, e) =>
            {
                this.Close();
            };

            this.BeginAnimation(Window.TopProperty, moveAnimation);
            this.BeginAnimation(Window.OpacityProperty, fadeAnimation);
        }

        private void PopupWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Если клик внутри окна - останавливаем таймер
            _hideTimer.Stop();
        }

        private void PopupWindow_Deactivated(object sender, EventArgs e)
        {
            // При деактивации запускаем таймер на закрытие
            if (!_hideTimer.IsEnabled && !_isClosing)
            {
                _hideTimer.Start();
            }
        }

        private void PopupWindow_LostFocus(object sender, RoutedEventArgs e)
        {
            // При потере фокуса запускаем таймер
            if (!_hideTimer.IsEnabled && !_isClosing)
            {
                _hideTimer.Start();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // Позволяем перетаскивать окно
                this.DragMove();
            }
        }

        private void DrawGrid()
        {
            // Вертикальные линии сетки
            for (int i = 0; i <= 10; i++)
            {
                var line = new Line
                {
                    X1 = i * 30,
                    Y1 = 0,
                    X2 = i * 30,
                    Y2 = 150,
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    StrokeThickness = 0.5
                };
                ChartCanvas.Children.Add(line);
            }

            // Горизонтальные линии сетки
            for (int i = 0; i <= 5; i++)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = i * 30,
                    X2 = 300,
                    Y2 = i * 30,
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    StrokeThickness = 0.5
                };
                ChartCanvas.Children.Add(line);
            }
        }

        private void InitializeChart()
        {
            lock (_monitor.PingHistory)
            {
                foreach (var pingResult in _monitor.PingHistory)
                {
                    _pingValues.Add(pingResult.RoundtripTime);
                }
            }
            UpdateChart();
        }

        private void SubscribeToEvents()
        {
            _monitor.StatusChanged += OnNetworkStatusChanged;
        }

        private void OnNetworkStatusChanged(object sender, Models.NetworkStatusChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _pingValues.Add(e.Ping);
                if (_pingValues.Count > 20)
                    _pingValues.RemoveAt(0);

                UpdateChart();

                CurrentPing.Text = e.Ping.HasValue ? $"{e.Ping} мс" : "--";
                StatusText.Text = e.IsOnline ? "Соединение стабильное" : "Нет соединения";
                ChartLine.Stroke = e.IsOnline ? Brushes.Green : Brushes.Red;

                var validValues = _pingValues.Where(v => v.HasValue).Select(v => v.Value).ToList();
                if (validValues.Count > 0)
                {
                    var avg = validValues.Average();
                    AvgPing.Text = $"{avg:F0} мс";
                }

                var total = _pingValues.Count;
                var failed = _pingValues.Count(v => !v.HasValue);
                var lossPercentage = total > 0 ? (failed * 100.0 / total) : 0;
                PacketLoss.Text = $"{lossPercentage:F1}%";
            });
        }
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Останавливаем таймер при клике внутри графика
            _hideTimer.Stop();
            e.Handled = true;
        }
        private void UpdateChart()
        {
            if (_pingValues == null || _pingValues.Count == 0)
                return;

            ChartLine.Points.Clear();

            double maxValue = 500;
            double width = 300;
            double height = 150;

            for (int i = 0; i < _pingValues.Count; i++)
            {
                if (_pingValues[i].HasValue)
                {
                    double x = (double)i / (_pingValues.Count - 1) * (width - 20) + 10;
                    double y = height - 10 - (_pingValues[i].Value / maxValue * (height - 20));
                    ChartLine.Points.Add(new Point(x, y));
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_monitor != null)
                _monitor.StatusChanged -= OnNetworkStatusChanged;

            base.OnClosed(e);
        }
    }
}