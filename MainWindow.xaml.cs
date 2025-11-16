using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using PomodoroTimer.Models;
using PomodoroTimer.Services;
using CommunityToolkit.WinUI.Notifications;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace PomodoroTimer
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private int _timeLeftSeconds;
        private bool _isRunning;
        private bool _isWorking = true; // true = Work, false = Rest

        private List<PomodoroPreset> _presets = new();
        private PomodoroPreset _currentPreset = new PomodoroPreset();

        private Dictionary<string, List<PomodoroStatsEntry>> _stats = new();
        private readonly string _todayKey = DateTime.Today.ToString("yyyy-MM-dd");
        private List<PomodoroStatsEntry> _todayEntries = new();

        private NotifyIcon _notifyIcon = null!;
        private double _currentPeriodDurationMinutes;
        private bool _reallyQuit;
        private string _lastTrayMinutes = "00";

        public bool IsWorking => _isWorking;

        public MainWindow()
        {
            InitializeComponent();

            // Presets
            _presets = ConfigService.LoadPresets();
            if (_presets.Count == 0)
            {
                _presets.Add(new PomodoroPreset { Name = "Default", WorkMinutes = 25, RestMinutes = 5 });
            }

            _currentPreset = _presets[0];
            PresetList.ItemsSource = _presets;
            PresetList.SelectedItem = _currentPreset;

            // Stats
            _stats = StatsService.LoadStats();
            if (!_stats.TryGetValue(_todayKey, out _todayEntries!))
            {
                _todayEntries = new List<PomodoroStatsEntry>();
                _stats[_todayKey] = _todayEntries;
            }
            DailyChart.Entries = _todayEntries;

            // Timer
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            // Tray
            SetupTrayIcon();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            EnableDarkTitleBar();
        }

        private void EnableDarkTitleBar()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int useDark = 1;

            // Windows 10 1809–20H2
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));

            // Windows 11 и новые Windows 10
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        #region Timer logic

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            StartTimer();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            PauseTimer();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopTimer();
        }

        public void StartTimer()
        {
            // Если уже идёт – ничего не делаем
            if (_isRunning)
                return;

            // Если таймер был на паузе (есть оставшееся время) – просто продолжаем
            if (_timeLeftSeconds > 0)
            {
                _isRunning = true;
                _timer.Start();
                StatusText.Text = _isWorking ? "Работа" : "Отдых";
                UpdateTimeDisplay();
                return;
            }

            // Иначе запускаем новый период с нуля
            int work = Math.Max(1, _currentPreset.WorkMinutes);
            int rest = Math.Max(1, _currentPreset.RestMinutes);
            int minutes = _isWorking ? work : rest;

            _timeLeftSeconds = minutes * 60;
            _currentPeriodDurationMinutes = minutes;
            _isRunning = true;
            _timer.Start();

            StatusText.Text = _isWorking ? "Работа" : "Отдых";
            UpdateTimeDisplay();
        }

        public void PauseTimer()
        {
            if (!_isRunning)
                return;

            _timer.Stop();
            _isRunning = false;
            StatusText.Text = "Пауза";
        }

        public void StopTimer()
        {
            _timer.Stop();
            _isRunning = false;
            _timeLeftSeconds = 0;
            _currentPeriodDurationMinutes = 0;

            TimeText.Text = "00:00";
            StatusText.Text = "Остановлено";

            _lastTrayMinutes = "00";
            UpdateTrayIcon("00", false);
            _notifyIcon.Text = "Pomodoro Timer";
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_timeLeftSeconds <= 0)
            {
                PeriodFinished();
                return;
            }

            _timeLeftSeconds--;
            UpdateTimeDisplay();
        }

        private void UpdateTimeDisplay()
        {
            int minutes = _timeLeftSeconds / 60;
            int seconds = _timeLeftSeconds % 60;
            string timeStr = $"{minutes:00}:{seconds:00}";

            TimeText.Text = timeStr;

            string minutesStr = $"{minutes:00}";
            bool isRed = _isWorking && _timeLeftSeconds > 0 && _timeLeftSeconds <= 5 * 60;

            if (minutesStr != _lastTrayMinutes)
            {
                _lastTrayMinutes = minutesStr;
                UpdateTrayIcon(minutesStr, isRed);
            }

            _notifyIcon.Text = $"{timeStr} – {(_isWorking ? "Работа" : "Отдых")} ({_currentPreset.Name})";
        }

        private void PeriodFinished()
        {
            _timer.Stop();
            _isRunning = false;
            var wasWorkPeriod = _isWorking;
            _timeLeftSeconds = 0;

            TimeText.Text = "00:00";

            RegisterCompletedPeriod(wasWorkPeriod);

            if (wasWorkPeriod)
            {
                System.Media.SystemSounds.Asterisk.Play();
                System.Media.SystemSounds.Asterisk.Play();

                StatusText.Text = "Рабочий период завершён";

                // Современное уведомление Windows 11
                ShowModernNotification(
                    "Рабочий период завершён! 🍅",
                    $"Таймер \"{_currentPreset.Name}\" завершён. Время отдохнуть!",
                    "work"
                );

                // Оставляем старое для совместимости
                _notifyIcon.ShowBalloonTip(
                    5000,
                    "Pomodoro Timer",
                    $"Рабочий таймер \"{_currentPreset.Name}\" завершён.",
                    ToolTipIcon.Info
                );

                _isWorking = false;
            }
            else
            {
                System.Media.SystemSounds.Asterisk.Play();

                StatusText.Text = "Период отдыха завершён";

                // Современное уведомление Windows 11
                ShowModernNotification(
                    "Отдых завершён! ⏰",
                    $"Таймер отдыха \"{_currentPreset.Name}\" завершён. Готовы к работе?",
                    "rest"
                );

                // Оставляем старое для совместимости
                _notifyIcon.ShowBalloonTip(
                    5000,
                    "Pomodoro Timer",
                    $"Таймер отдыха \"{_currentPreset.Name}\" завершён.",
                    ToolTipIcon.Info
                );

                _isWorking = true;
            }

            UpdateTrayIcon("00", false);

            if (AutoContinueCheck.IsChecked == true)
            {
                StartTimer();
            }
        }

        private void ShowModernNotification(string title, string message, string type)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .Show();
            }
            catch
            {
                // Если не получилось показать современное уведомление,
                // просто игнорируем (старое balloon tip покажется)
            }
        }

        #endregion

        #region Stats

        private void RegisterCompletedPeriod(bool wasWorkPeriod)
        {
            var now = DateTime.Now;

            double duration = _currentPeriodDurationMinutes > 0
                ? _currentPeriodDurationMinutes
                : (wasWorkPeriod
                    ? _currentPreset.WorkMinutes
                    : _currentPreset.RestMinutes);

            if (duration <= 0)
            {
                duration = wasWorkPeriod
                    ? Math.Max(1, _currentPreset.WorkMinutes)
                    : Math.Max(1, _currentPreset.RestMinutes);
            }

            double endMinutes = now.Hour * 60 + now.Minute + now.Second / 60.0;
            double startMinutes = endMinutes - duration;
            if (startMinutes < 0)
            {
                startMinutes = 0;
            }

            var entry = new PomodoroStatsEntry
            {
                // в поле TimeMinutes теперь кладём ВРЕМЯ НАЧАЛА
                TimeMinutes = startMinutes,
                DurationMinutes = duration,
                Type = wasWorkPeriod ? "work" : "rest"
            };

            _todayEntries.Add(entry);
            DailyChart.Entries = null!;
            DailyChart.Entries = _todayEntries;
            StatsService.SaveStats(_stats);
            _currentPeriodDurationMinutes = 0;
        }

        #endregion

        #region Presets

        private void PresetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetList.SelectedItem is PomodoroPreset p)
            {
                _currentPreset = p;
            }
        }

        public void SelectPresetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            var preset = _presets.FirstOrDefault(
                p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (preset != null)
            {
                _currentPreset = preset;
                PresetList.SelectedItem = preset;
            }
        }

        private void AddPreset_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PresetWindow
            {
                Owner = this
            };

            bool? result = dlg.ShowDialog();
            if (result == true && dlg.Result != null)
            {
                var p = dlg.Result;
                _presets.Add(p);
                PresetList.Items.Refresh();
                ConfigService.SavePresets(_presets);
            }
        }

        private void EditPreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetList.SelectedItem is not PomodoroPreset p)
                return;

            var dlg = new PresetWindow(p)
            {
                Owner = this
            };

            bool? result = dlg.ShowDialog();
            if (result == true && dlg.Result != null)
            {
                var res = dlg.Result;
                p.Name = res.Name;
                p.WorkMinutes = res.WorkMinutes;
                p.RestMinutes = res.RestMinutes;

                PresetList.Items.Refresh();
                ConfigService.SavePresets(_presets);
            }
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetList.SelectedItem is not PomodoroPreset p)
                return;

            if (_presets.Count <= 1)
            {
                MessageBox.Show(
                    "Нельзя удалить последний пресет.",
                    "Presets",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            var result = MessageBox.Show(
                $"Удалить пресет \"{p.Name}\"?",
                "Presets",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            _presets.Remove(p);
            PresetList.Items.Refresh();

            if (_presets.Count > 0)
            {
                _currentPreset = _presets[0];
                PresetList.SelectedItem = _currentPreset;
            }

            ConfigService.SavePresets(_presets);
        }

        #endregion

        #region Tray

        private void SetupTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Visible = true,
                Icon = CreateTrayIcon("PT", false),
                Text = "Pomodoro Timer"
            };

            var menu = new ContextMenuStrip();

            var startItem = new ToolStripMenuItem("Start", null, (_, _) => StartTimer());
            var pauseItem = new ToolStripMenuItem("Pause", null, (_, _) => PauseTimer());
            var stopItem = new ToolStripMenuItem("Stop", null, (_, _) => StopTimer());
            var showItem = new ToolStripMenuItem("Show", null, (_, _) => ShowWindow());
            var quitItem = new ToolStripMenuItem("Quit", null, (_, _) => QuitFromExternal());

            menu.Items.Add(startItem);
            menu.Items.Add(pauseItem);
            menu.Items.Add(stopItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(showItem);
            menu.Items.Add(quitItem);

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private void NotifyIcon_MouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ShowWindow();
            }
        }

        public void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
        }

        public void QuitFromExternal()
        {
            _reallyQuit = true;
            StatsService.SaveStats(_stats);
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        private System.Drawing.Icon CreateTrayIcon(string minutesText, bool red)
        {
            int size = 256; // Увеличен размер для лучшего качества

            using var bmp = new System.Drawing.Bitmap(
                size,
                size,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                string text = string.IsNullOrWhiteSpace(minutesText) ? "00" : minutesText.Trim();

                // Используем более современный и чёткий шрифт
                using var font = new System.Drawing.Font(
                    "Segoe UI",
                    165f, // Увеличен размер для лучшей читаемости
                    System.Drawing.FontStyle.Bold,
                    System.Drawing.GraphicsUnit.Pixel);

                var mainColor = red
                    ? System.Drawing.Color.FromArgb(255, 235, 87, 87) // Более яркий красный
                    : System.Drawing.Color.FromArgb(255, 255, 255, 255); // Чистый белый

                // Измеряем размер текста с дополнительными параметрами для точности
                var format = new StringFormat(StringFormat.GenericDefault)
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                
                var textSize = g.MeasureString(text, font, new PointF(0, 0), format);

                // Центрируем с учётом дополнительных отступов и сдвигом вверх
                // Добавляем 10% ширины для гарантии, что цифры не обрежутся
                float x = (size - textSize.Width) / 2f - textSize.Width * 0.05f;
                float y = (size - textSize.Height) / 2f - 8f; // Сдвиг вверх

                // Создаём путь для текста
                using var path = new GraphicsPath();
                path.AddString(
                    text,
                    font.FontFamily,
                    (int)font.Style,
                    g.DpiY * font.Size / 72,
                    new PointF(x, y),
                    StringFormat.GenericDefault);

                // Добавляем мягкую тень для глубины
                using var shadowBrush = new SolidBrush(System.Drawing.Color.FromArgb(160, 0, 0, 0));
                var shadowMatrix = new Matrix();
                shadowMatrix.Translate(3, 4);
                path.Transform(shadowMatrix);
                g.FillPath(shadowBrush, path);

                // Возвращаем путь на место
                shadowMatrix.Reset();
                shadowMatrix.Translate(-3, -4);
                path.Transform(shadowMatrix);

                // Рисуем контур для чёткости
                using var outlinePen = new Pen(System.Drawing.Color.FromArgb(100, 0, 0, 0), 2f);
                g.DrawPath(outlinePen, path);

                // Основной текст с градиентом
                using var textBrush = new SolidBrush(mainColor);
                g.FillPath(textBrush, path);

                // Добавляем лёгкий блик сверху для объёмности
                if (!red)
                {
                    var highlightRect = new RectangleF(x, y, textSize.Width, textSize.Height / 2);
                    using var highlightBrush = new LinearGradientBrush(
                        highlightRect,
                        System.Drawing.Color.FromArgb(40, 255, 255, 255),
                        System.Drawing.Color.FromArgb(0, 255, 255, 255),
                        LinearGradientMode.Vertical);
                    
                    g.FillPath(highlightBrush, path);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            var icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);
            return icon;
        }

        private void UpdateTrayIcon(string minutesText, bool red)
        {
            if (_notifyIcon == null)
                return;

            var newIcon = CreateTrayIcon(minutesText, red);
            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = newIcon;
            oldIcon?.Dispose();
        }

        #endregion

        #region Hotkeys

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

            if (!ctrl || !alt)
                return;

            if (e.Key == Key.P)
            {
                StartTimer();
                e.Handled = true;
            }
            else if (e.Key == Key.S)
            {
                StopTimer();
                e.Handled = true;
            }
            else if (e.Key == Key.R)
            {
                ToggleRest();
                e.Handled = true;
            }
        }

        public void ToggleRest()
        {
            _isWorking = !_isWorking;
            StopTimer();
            StartTimer();
        }

        #endregion

        #region Closing

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_reallyQuit)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }

        #endregion
    }
}