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
using System.Windows.Controls;

namespace NetTray
{
    public partial class PopupWindow : Window
    {
        private readonly NetworkMonitor _monitor;
        private readonly List<PingPoint> _pingPoints = new List<PingPoint>();
        private DispatcherTimer _hideTimer;
        private const int AnimationMS = 250;
        private const int HideDelayMS = 150;
        private bool _isClosing = false;

        // Константы для графика
        private const int MAX_POINTS = 20;         // Сколько точек хранить
        private const double MARGIN_TOP = 20;      // Отступ сверху
        private const double MARGIN_BOTTOM = 20;   // Отступ снизу
        private const double PADDING = 10;         // Запас для масштабирования

        // Класс для хранения точек с цветом
        private class PingPoint
        {
            public long? Value { get; set; }
            public bool IsOnline { get; set; }
            public DateTime Timestamp { get; set; }
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
            var screen = SystemParameters.WorkArea;

            double targetTop = screen.Bottom - this.Height - 10;
            double startTop = screen.Bottom + 10;

            this.Left = screen.Right - this.Width - 10;
            this.Top = startTop;
            this.Opacity = 0;

            this.Show();
            this.Activate();
            this.Focus();

            var moveAnimation = new DoubleAnimation
            {
                From = startTop,
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(AnimationMS),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

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

            var moveAnimation = new DoubleAnimation
            {
                From = this.Top,
                To = endTop,
                Duration = TimeSpan.FromMilliseconds(AnimationMS),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

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
            _hideTimer.Stop();
        }

        private void PopupWindow_Deactivated(object sender, EventArgs e)
        {
            if (!_hideTimer.IsEnabled && !_isClosing)
            {
                _hideTimer.Start();
            }
        }

        private void PopupWindow_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_hideTimer.IsEnabled && !_isClosing)
            {
                _hideTimer.Start();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
        private void AddValueLabels(double minValue, double maxValue)
        {
            // Очищаем старые подписи
            var labelsToRemove = ChartCanvas.Children
                .OfType<TextBlock>()
                .ToList();

            foreach (var label in labelsToRemove)
            {
                ChartCanvas.Children.Remove(label);
            }

            // Добавляем минимальное значение слева
            var minLabel = new TextBlock
            {
                Text = $"{minValue:F0} мс",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            Canvas.SetLeft(minLabel, 5);
            Canvas.SetBottom(minLabel, MARGIN_BOTTOM - 15);
            ChartCanvas.Children.Add(minLabel);

            // Добавляем максимальное значение слева
            var maxLabel = new TextBlock
            {
                Text = $"{maxValue:F0} мс",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            Canvas.SetLeft(maxLabel, 5);
            Canvas.SetTop(maxLabel, MARGIN_TOP - 15);
            ChartCanvas.Children.Add(maxLabel);

            // Добавляем среднее значение посередине
            var avgValue = (minValue + maxValue) / 2;
            var avgLabel = new TextBlock
            {
                Text = $"{avgValue:F0} мс",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            Canvas.SetLeft(avgLabel, 5);
            Canvas.SetTop(avgLabel, MARGIN_TOP + (150 - MARGIN_TOP - MARGIN_BOTTOM) / 2 - 8);
            ChartCanvas.Children.Add(avgLabel);
        }
        private void DrawGrid()
        {
            // Очищаем предыдущую сетку
            ChartCanvas.Children.Clear();

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

            // Добавляем линию графика в конец, чтобы она была поверх сетки
            ChartCanvas.Children.Add(ChartLine);
        }

        private void InitializeChart()
        {
            lock (_monitor.PingHistory)
            {
                foreach (var pingResult in _monitor.PingHistory)
                {
                    _pingPoints.Add(new PingPoint
                    {
                        Value = pingResult.RoundtripTime,
                        IsOnline = pingResult.IsSuccess,
                        Timestamp = pingResult.Timestamp
                    });
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
                // Добавляем новую точку
                _pingPoints.Add(new PingPoint
                {
                    Value = e.Ping,
                    IsOnline = e.IsOnline,
                    Timestamp = DateTime.Now
                });

                // Ограничиваем количество точек
                if (_pingPoints.Count > MAX_POINTS)
                    _pingPoints.RemoveAt(0);

                UpdateChart();

                // Обновляем статистику
                CurrentPing.Text = e.Ping.HasValue ? $"{e.Ping} мс" : "--";
                StatusText.Text = e.IsOnline ? "Соединение стабильное" : "Нет соединения";

                // Рассчитываем средний пинг (только онлайн значения)
                var validValues = _pingPoints
                    .Where(p => p.IsOnline && p.Value.HasValue)
                    .Select(p => p.Value.Value)
                    .ToList();

                if (validValues.Count > 0)
                {
                    var avg = validValues.Average();
                    AvgPing.Text = $"{avg:F0} мс";
                }
                else
                {
                    AvgPing.Text = "--";
                }

                // Рассчитываем потери
                var total = _pingPoints.Count;
                var failed = _pingPoints.Count(p => !p.IsOnline);
                var lossPercentage = total > 0 ? (failed * 100.0 / total) : 0;
                PacketLoss.Text = $"{lossPercentage:F1}%";
            });
        }

        private void UpdateChart()
        {
            if (_pingPoints == null || _pingPoints.Count < 2)
                return;

            ChartLine.Points.Clear();

            var onlineValues = _pingPoints
                .Where(p => p.IsOnline && p.Value.HasValue)
                .Select(p => p.Value.Value)
                .ToList();

            if (onlineValues.Count == 0)
            {
                DrawColoredLine();
                return;
            }

            double minValue = onlineValues.Min();
            double maxValue = onlineValues.Max();

            // Добавляем запас в 3 мс
            minValue = Math.Max(0, minValue - 3);
            maxValue += 3;

            // Если min и max слишком близки, расширяем диапазон
            if (maxValue - minValue < 10)
            {
                double middle = (minValue + maxValue) / 2;
                minValue = Math.Max(0, middle - 5);
                maxValue = middle + 5;
            }

            double width = 300;
            double height = 150;
            double chartHeight = height - MARGIN_TOP - MARGIN_BOTTOM;
            double valueRange = maxValue - minValue;

            // Добавляем подписи значений
            AddValueLabels(minValue, maxValue);

            // Рисуем цветную линию
            DrawColoredLine(minValue, valueRange, width, chartHeight);
        }

        private void DrawColoredLine(double minValue = 0, double valueRange = 100, double width = 300, double chartHeight = 110)
        {
            // Очищаем все линии
            var linesToRemove = ChartCanvas.Children
                .OfType<Polyline>()
                .Where(p => p != ChartLine)
                .ToList();

            foreach (var line in linesToRemove)
            {
                ChartCanvas.Children.Remove(line);
            }

            // Разделяем точки на сегменты по статусу (онлайн/оффлайн)
            var segments = new List<List<PingPoint>>();
            var currentSegment = new List<PingPoint>();

            for (int i = 0; i < _pingPoints.Count; i++)
            {
                currentSegment.Add(_pingPoints[i]);

                // Если следующая точка другого статуса или это последняя точка
                if (i == _pingPoints.Count - 1 ||
                    _pingPoints[i].IsOnline != _pingPoints[i + 1].IsOnline)
                {
                    segments.Add(new List<PingPoint>(currentSegment));
                    currentSegment.Clear();
                }
            }

            // Рисуем каждый сегмент своим цветом
            foreach (var segment in segments)
            {
                if (segment.Count == 0) continue;

                var polyline = new Polyline
                {
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round
                };

                // Выбираем цвет в зависимости от статуса
                polyline.Stroke = segment[0].IsOnline ? Brushes.Green : Brushes.Red;

                // Добавляем точки
                for (int i = 0; i < segment.Count; i++)
                {
                    int globalIndex = _pingPoints.IndexOf(segment[i]);
                    double x = (double)globalIndex / (_pingPoints.Count - 1) * (width - PADDING * 2) + PADDING;

                    if (segment[i].IsOnline && segment[i].Value.HasValue)
                    {
                        // Онлайн точка: нормальная позиция
                        double normalizedY = (segment[i].Value.Value - minValue) / valueRange;
                        double y = MARGIN_TOP + chartHeight - (normalizedY * chartHeight);
                        polyline.Points.Add(new Point(x, y));
                    }
                    else
                    {
                        // Оффлайн точка: рисуем внизу
                        double y = MARGIN_TOP + chartHeight + 5; // Немного ниже графика
                        polyline.Points.Add(new Point(x, y));
                    }
                }

                // Добавляем линию на канвас
                ChartCanvas.Children.Add(polyline);
            }

            // Добавляем горизонтальную линию посередине для ориентира
            var middleLine = new Line
            {
                X1 = PADDING,
                Y1 = MARGIN_TOP + chartHeight / 2,
                X2 = width - PADDING,
                Y2 = MARGIN_TOP + chartHeight / 2,
                Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 5, 5 }
            };
            ChartCanvas.Children.Add(middleLine);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_monitor != null)
                _monitor.StatusChanged -= OnNetworkStatusChanged;

            base.OnClosed(e);
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _hideTimer.Stop();
            e.Handled = true;
        }
    }
}