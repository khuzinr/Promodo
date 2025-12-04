using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Forms = System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PomodoroTimer.Models;
using PomodoroTimer.Services;
using CommunityToolkit.WinUI.Notifications;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using DrawingPen = System.Drawing.Pen;

namespace PomodoroTimer
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private int _timeLeftSeconds;
        private bool _isRunning;
        private bool _isWorking = true;
        private DateTime? _periodStartTime; // Добавлено для точного учета времени

        private List<PomodoroPreset> _presets = new();
        private PomodoroPreset _currentPreset = new();

        private List<TimerButtonDefinition> _timerButtons = new();
        private TimerButtonDefinition? _activeButton;

        private Dictionary<string, List<PomodoroStatsEntry>> _stats = new();
        private string _currentDayKey = DateTime.Today.ToString("yyyy-MM-dd");
        private List<PomodoroStatsEntry> _todayEntries = new();
        private DateTime _viewedDate = DateTime.Today;
        private string _viewedDateKey = string.Empty;

        private readonly DispatcherTimer _dayCheckTimer;

        private Forms.NotifyIcon _notifyIcon = null!;
        private bool _reallyQuit;
        private string _lastTrayMinutes = "00";
        private Icon? _currentTrayIcon; // Для освобождения ресурсов
        private StatsWindow? _statsWindow;
        private bool _restoreWindowOnFinish = true;
        private WindowSettings _windowSettings = new();
        private bool _pinWindowWhenIdle;

        public bool IsWorking => _isWorking;
        private bool HasActivePeriod => _timeLeftSeconds > 0 || _isRunning;

        private TimerButtonDefinition ActiveButton => _activeButton ?? _timerButtons.FirstOrDefault() ?? new TimerButtonDefinition
        {
            Id = "default-work",
            Name = "Работа",
            BackgroundColorHex = "#4CAF50",
            TextColorHex = "#FFFFFF",
            IsRest = false
        };

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

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

            // Timer buttons
            _timerButtons = ConfigService.LoadTimerButtons();
            ButtonList.ItemsSource = _timerButtons;
            _activeButton = _timerButtons.FirstOrDefault();

            // Stats
            _stats = StatsService.LoadStats();
            NormalizeStats();
            _currentDayKey = FormatDateKey(DateTime.Today);
            _todayEntries = GetOrCreateEntriesForKey(_currentDayKey);
            _viewedDate = DateTime.Today;
            _viewedDateKey = _currentDayKey;
            UpdateDailyChart();
            UpdateSummaryStats();

            // Timer
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            _dayCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _dayCheckTimer.Tick += (_, _) => EnsureCurrentDay();
            _dayCheckTimer.Start();

            // Tray
            SetupTrayIcon();
            UpdateStartPauseButton();

            EnsureActiveButton();
            UpdateStatusText();

            _windowSettings = ConfigService.LoadWindowSettings();
            _pinWindowWhenIdle = _windowSettings.PinWindowWhenIdle;
            if (PinWindowWhenIdleCheck != null)
            {
                PinWindowWhenIdleCheck.IsChecked = _pinWindowWhenIdle;
            }

            _restoreWindowOnFinish = RestoreFromTrayCheck?.IsChecked == true;
            if (RestoreFromTrayCheck != null)
            {
                RestoreFromTrayCheck.Checked += RestoreFromTrayCheck_OnChanged;
                RestoreFromTrayCheck.Unchecked += RestoreFromTrayCheck_OnChanged;
            }

            StateChanged += MainWindow_StateChanged;
            ApplyIdlePinning();
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

            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }

        #region Timer logic

        private void StartPause_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                PauseTimer();
            }
            else
            {
                StartTimer();
            }
        }

        private void TimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: TimerButtonDefinition def })
            {
                ActivateButton(def, startImmediately: true);
            }
        }

        private void ConfigureButtons_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new TimerButtonSettingsWindow(_timerButtons)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                _timerButtons = dlg.Result.ToList();
                ConfigService.SaveTimerButtons(_timerButtons);
                ButtonList.ItemsSource = _timerButtons;

                if (_activeButton != null)
                {
                    _activeButton = _timerButtons.FirstOrDefault(b => b.Id == _activeButton.Id)
                                     ?? _timerButtons.FirstOrDefault();
                }

                EnsureActiveButton();
                UpdateStatusText();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopTimer();
        }

        public void ToggleRest()
        {
            var restButton = _timerButtons.FirstOrDefault(b => b.IsRest);
            var workButton = _timerButtons.FirstOrDefault(b => !b.IsRest);

            if (_activeButton != null && _activeButton.IsRest && workButton != null)
            {
                ActivateButton(workButton, startImmediately: true);
            }
            else if (restButton != null)
            {
                ActivateButton(restButton, startImmediately: true);
            }
        }

        public void ActivateRestAndStart()
        {
            var restButton = _timerButtons.FirstOrDefault(b => b.IsRest);
            if (restButton != null)
            {
                ActivateButton(restButton, startImmediately: true);
            }
        }

        public void StartTimer()
        {
            if (_isRunning)
                return;

            EnsureActiveButton();
            _isWorking = !ActiveButton.IsRest;

            // Если таймер был на паузе (есть оставшееся время) - просто продолжаем
            if (_timeLeftSeconds > 0)
            {
                _isRunning = true;
                _timer.Start();

                // Восстанавливаем время начала периода с учетом уже прошедшего времени
                if (_periodStartTime == null)
                {
                    int totalMinutes = _isWorking ? _currentPreset.WorkMinutes : _currentPreset.RestMinutes;
                    int elapsedSeconds = (totalMinutes * 60) - _timeLeftSeconds;
                    _periodStartTime = DateTime.Now.AddSeconds(-elapsedSeconds);
                }

                UpdateStatusText();
                UpdateTimeDisplay();
                UpdateStartPauseButton();
                HideToTrayIfRunning();
                return;
            }

            // Иначе запускаем новый период с нуля
            int work = Math.Max(1, _currentPreset.WorkMinutes);
            int rest = Math.Max(1, _currentPreset.RestMinutes);
            int minutes = _isWorking ? work : rest;

            _timeLeftSeconds = minutes * 60;
            _periodStartTime = DateTime.Now; // Запоминаем точное время старта
            _isRunning = true;
            _timer.Start();

            UpdateStatusText();
            UpdateTimeDisplay();
            UpdateStartPauseButton();
            HideToTrayIfRunning();
        }

        public void PauseTimer()
        {
            if (!_isRunning)
                return;

            _timer.Stop();
            _isRunning = false;
            StatusText.Text = "Пауза";
            UpdateStartPauseButton();
        }

        public void StopTimer()
        {
            _timer.Stop();
            _isRunning = false;
            _timeLeftSeconds = 0;
            _periodStartTime = null;

            TimeText.Text = "00:00";
            StatusText.Text = "Остановлено";
            TimeText.Foreground = ResolveBrush(ActiveButton.BackgroundColorHex, Colors.White);
            StatusText.Foreground = ResolveBrush(ActiveButton.TextColorHex, Colors.White);

            UpdateTrayIcon("00", false);
            _notifyIcon.Text = "Pomodoro Timer";
            UpdateStartPauseButton();

            ApplyIdlePinning();
        }

        private void ActivateButton(TimerButtonDefinition definition, bool startImmediately)
        {
            _activeButton = definition;
            _isWorking = !definition.IsRest;

            _timer.Stop();
            _isRunning = false;
            _timeLeftSeconds = 0;
            _periodStartTime = null;

            UpdateStatusText();
            UpdateTimeDisplay();

            if (startImmediately)
            {
                StartTimer();
            }
        }

        private void EnsureActiveButton()
        {
            if (_activeButton == null)
            {
                _activeButton = ActiveButton;
            }
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

            UpdateTrayIcon(minutesStr, isRed);
            _notifyIcon.Text = $"{timeStr} — {ActiveButton.Name}";
        }

        private void UpdateStatusText()
        {
            var button = ActiveButton;
            string prefix = button.IsRest ? "Отдых" : "Работа";
            StatusText.Text = string.IsNullOrWhiteSpace(button.Name)
                ? prefix
                : $"{prefix}: {button.Name}";

            var timerBrush = ResolveBrush(button.BackgroundColorHex, System.Windows.Media.Colors.White);
            TimeText.Foreground = timerBrush;
            StatusText.Foreground = ResolveBrush(button.TextColorHex, System.Windows.Media.Colors.White);
        }

        private void PeriodFinished()
        {
            _timer.Stop();
            _isRunning = false;
            _timeLeftSeconds = 0;

            TimeText.Text = "00:00";

            var button = ActiveButton;
            if (_isWorking && !button.IsRest)
            {
                RegisterCompletedPomodoro();

                System.Media.SystemSounds.Asterisk.Play();
                System.Media.SystemSounds.Asterisk.Play();

                StatusText.Text = $"Период \"{button.Name}\" завершён";

                ShowModernNotification(
                    "Рабочий период завершён! 🍅",
                    $"Таймер \"{button.Name}\" завершён. Время отдохнуть!",
                    "work"
                );
            }
            else
            {
                RegisterCompletedRest();

                System.Media.SystemSounds.Asterisk.Play();

                StatusText.Text = $"Отдых \"{button.Name}\" завершён";

                ShowModernNotification(
                    "Отдых завершён! ⏰",
                    $"Таймер отдыха \"{button.Name}\" завершён. Готовы к работе?",
                    "rest"
                );
            }

            _periodStartTime = null; // Сбрасываем время начала
            UpdateTrayIcon("00", false);
            UpdateStartPauseButton();

            if (_restoreWindowOnFinish)
            {
                ShowWindow();
            }

            ApplyIdlePinning();

            if (AutoContinueCheck.IsChecked == true)
            {
                // Небольшая задержка перед автостартом для лучшего UX
                var autoStartTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                autoStartTimer.Tick += (s, e) =>
                {
                    autoStartTimer.Stop();
                    StartTimer();
                };
                autoStartTimer.Start();
            }
        }

        private void UpdateStartPauseButton()
        {
            if (StartPauseButton == null)
                return;

            var tooltipText = "Запуск/Пауза таймера (Ctrl+Alt+D)\nОстановка и сброс: Ctrl+Alt+S";

            StartPauseButton.ToolTip = tooltipText;
            AutomationProperties.SetName(StartPauseButton, _isRunning ? "Пауза таймера" : "Запуск таймера");
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
                // Fallback - если не получилось показать современное уведомление
            }
        }

        #endregion

        #region Stats

        private void RegisterCompletedPomodoro()
        {
            AddStatsEntry(ActiveButton, _currentPreset.WorkMinutes);
        }

        private void RegisterCompletedRest()
        {
            AddStatsEntry(ActiveButton, _currentPreset.RestMinutes);
        }

        private void AddStatsEntry(TimerButtonDefinition button, double durationMinutes)
        {
            EnsureCurrentDay();

            if (_periodStartTime == null)
            {
                _periodStartTime = DateTime.Now.AddMinutes(-durationMinutes);
            }

            var startTime = _periodStartTime.Value;
            double startMinutes = startTime.Hour * 60 + startTime.Minute + startTime.Second / 60.0;

            var entry = new PomodoroStatsEntry
            {
                TimeMinutes = startMinutes,
                DurationMinutes = durationMinutes,
                Type = button.Name,
                ColorHex = button.BackgroundColorHex,
                IsRest = button.IsRest
            };

            _todayEntries.Add(entry);

            if (_viewedDateKey == _currentDayKey)
            {
                UpdateDailyChart();
            }

            StatsService.SaveStats(_stats);
            _statsWindow?.RefreshData();
            UpdateSummaryStats();
        }

        private void NormalizeStats()
        {
            foreach (var dayEntries in _stats.Values)
            {
                if (dayEntries == null)
                    continue;

                foreach (var entry in dayEntries)
                {
                    bool isRest = IsRestEntry(entry);
                    entry.IsRest = isRest;

                    if (string.IsNullOrWhiteSpace(entry.ColorHex))
                    {
                        entry.ColorHex = isRest ? "#9B59B6" : "#5AC85A";
                    }
                }
            }
        }

        private static string FormatDateKey(DateTime date)
            => date.ToString("yyyy-MM-dd");

        private List<PomodoroStatsEntry> GetOrCreateEntriesForKey(string key)
        {
            if (!_stats.TryGetValue(key, out var entries) || entries == null)
            {
                entries = new List<PomodoroStatsEntry>();
                _stats[key] = entries;
            }

            return entries;
        }

        private void UpdateDailyChart()
        {
            if (string.IsNullOrEmpty(_viewedDateKey))
            {
                _viewedDateKey = FormatDateKey(_viewedDate);
            }

            var entries = GetOrCreateEntriesForKey(_viewedDateKey);

            DailyChart.Entries = null!;
            DailyChart.Entries = entries;

            HistoryDateText.Text = _viewedDate.ToString(
                "dd MMMM yyyy",
                System.Globalization.CultureInfo.CurrentUICulture);
        }

        private void UpdateSummaryStats()
        {
            double todayWork = SumMinutesForType(_currentDayKey, false);
            double todayRest = SumMinutesForType(_currentDayKey, true);
            double rollingAverage = CalculateRollingAverageWorkMinutes(3);

            if (SummaryTotalsText != null)
            {
                SummaryTotalsText.Text = string.Join(
                    "/",
                    FormatMinutes(todayWork),
                    FormatMinutes(todayRest),
                    FormatMinutes(rollingAverage));
            }
        }

        private double SumMinutesForType(string key, bool isRestType)
        {
            if (!_stats.TryGetValue(key, out var entries) || entries == null)
                return 0;

            return entries
                .Where(e => IsRestEntry(e) == isRestType)
                .Sum(e => e.DurationMinutes);
        }

        private double CalculateRollingAverageWorkMinutes(int workdaysCount)
        {
            int collected = 0;
            double totalMinutes = 0;
            var cursor = DateTime.Today;
            int guard = 0;

            while (collected < workdaysCount && guard < 90)
            {
                if (IsWorkday(cursor))
                {
                    string key = FormatDateKey(cursor);
                    totalMinutes += SumMinutesForType(key, false);
                    collected++;
                }

                cursor = cursor.AddDays(-1);
                guard++;
            }

            if (collected == 0)
                return 0;

            return totalMinutes / collected;
        }

        private static bool IsWorkday(DateTime date)
        {
            return date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        }

        private static string FormatMinutes(double minutes)
        {
            var span = TimeSpan.FromMinutes(minutes);
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}";
        }

        private static bool IsRestEntry(PomodoroStatsEntry entry)
        {
            if (entry.IsRest)
                return true;

            if (string.IsNullOrWhiteSpace(entry.Type))
                return false;

            return string.Equals(entry.Type, "rest", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(entry.Type, "отдых", StringComparison.OrdinalIgnoreCase);
        }

        private void RestoreFromTrayCheck_OnChanged(object sender, RoutedEventArgs e)
        {
            _restoreWindowOnFinish = RestoreFromTrayCheck?.IsChecked == true;
        }

        private void EnsureCurrentDay()
        {
            var today = DateTime.Today;
            var newKey = FormatDateKey(today);

            if (_currentDayKey == newKey)
                return;

            var previousKey = _currentDayKey;
            bool wasViewingCurrent = string.IsNullOrEmpty(_viewedDateKey) || _viewedDateKey == previousKey;

            _currentDayKey = newKey;
            _todayEntries = GetOrCreateEntriesForKey(newKey);

            if (wasViewingCurrent)
            {
                _viewedDate = today;
                _viewedDateKey = newKey;
            }

            UpdateDailyChart();
            UpdateSummaryStats();
        }

        private void ChangeViewedDay(int offset)
        {
            if (offset == 0)
                return;

            var candidate = _viewedDate.AddDays(offset);
            var today = DateTime.Today;

            if (candidate > today)
            {
                candidate = today;
            }

            if (candidate == _viewedDate)
                return;

            _viewedDate = candidate;
            _viewedDateKey = FormatDateKey(candidate);
            UpdateDailyChart();
        }

        private static SolidColorBrush ResolveBrush(string hex, System.Windows.Media.Color fallback)
        {
            try
            {
                var converter = new BrushConverter();
                if (converter.ConvertFromString(hex) is SolidColorBrush brush)
                    return brush;
            }
            catch
            {
                // ignore
            }

            return new SolidColorBrush(fallback);
        }

        private void OpenStatsWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_statsWindow == null)
            {
                _statsWindow = new StatsWindow(_stats)
                {
                    Owner = this
                };
                _statsWindow.Closed += (_, _) => _statsWindow = null;
                _statsWindow.Show();
            }
            else
            {
                if (_statsWindow.WindowState == WindowState.Minimized)
                {
                    _statsWindow.WindowState = WindowState.Normal;
                }

                _statsWindow.Activate();
            }
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
            _notifyIcon = new Forms.NotifyIcon
            {
                Visible = true,
                Text = "Pomodoro Timer"
            };

            // Создаем начальную иконку с "00"
            _currentTrayIcon = CreateTrayIcon("00", false);
            _notifyIcon.Icon = _currentTrayIcon;

            var menu = new Forms.ContextMenuStrip();

            var startItem = new Forms.ToolStripMenuItem("Start", null, (_, _) => Dispatcher.Invoke(StartTimer));
            var pauseItem = new Forms.ToolStripMenuItem("Pause", null, (_, _) => Dispatcher.Invoke(PauseTimer));
            var stopItem = new Forms.ToolStripMenuItem("Stop", null, (_, _) => Dispatcher.Invoke(StopTimer));
            var showItem = new Forms.ToolStripMenuItem("Show", null, (_, _) => Dispatcher.Invoke(ShowWindow));
            var quitItem = new Forms.ToolStripMenuItem("Quit", null, (_, _) => Dispatcher.Invoke(QuitFromExternal));

            menu.Items.Add(startItem);
            menu.Items.Add(pauseItem);
            menu.Items.Add(stopItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(showItem);
            menu.Items.Add(quitItem);

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private void NotifyIcon_MouseClick(object? sender, Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(ShowWindow);
            }
        }

        public void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;

            ApplyIdlePinning();
        }

        public void QuitFromExternal()
        {
            _reallyQuit = true;
            StatsService.SaveStats(_stats);
            _notifyIcon.Visible = false;
            
            // Освобождаем иконку
            if (_currentTrayIcon != null)
            {
                _currentTrayIcon.Dispose();
                _currentTrayIcon = null;
            }
            
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void UpdateTrayIcon(string minutesText, bool red)
        {
            if (minutesText == _lastTrayMinutes)
                return;

            _lastTrayMinutes = minutesText;

            // Освобождаем старую иконку
            if (_currentTrayIcon != null)
            {
                _currentTrayIcon.Dispose();
            }

            _currentTrayIcon = CreateTrayIcon(minutesText, red);
            _notifyIcon.Icon = _currentTrayIcon;
        }

        private void HideToTrayIfRunning()
        {
            if (_isRunning)
            {
                Hide();
            }
        }

        private Icon CreateTrayIcon(string minutesText, bool red)
        {
            int size = 256;

            using var bmp = new Bitmap(
                size,
                size,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                string text = string.IsNullOrWhiteSpace(minutesText) ? "00" : minutesText.Trim();

                using var font = new Font(
                    "Segoe UI",
                    180f,
                    System.Drawing.FontStyle.Bold,
                    GraphicsUnit.Pixel);

                var mainColor = red
                    ? System.Drawing.Color.FromArgb(255, 235, 87, 87)
                    : System.Drawing.Color.FromArgb(255, 255, 255, 255);

                var format = new StringFormat(StringFormat.GenericDefault)
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                
                var textSize = g.MeasureString(text, font, new PointF(0, 0), format);

                float x = (size - textSize.Width) / 2f - textSize.Width * 0.16f;
                float y = (size - textSize.Height) / 2f - 8f;

                using var path = new GraphicsPath();
                path.AddString(
                    text,
                    font.FontFamily,
                    (int)font.Style,
                    g.DpiY * font.Size / 72,
                    new PointF(x, y),
                    StringFormat.GenericDefault);

                using var shadowBrush = new SolidBrush(System.Drawing.Color.FromArgb(160, 0, 0, 0));
                var shadowMatrix = new System.Drawing.Drawing2D.Matrix();
                shadowMatrix.Translate(3, 4);
                path.Transform(shadowMatrix);
                g.FillPath(shadowBrush, path);

                shadowMatrix.Reset();
                shadowMatrix.Translate(-3, -4);
                path.Transform(shadowMatrix);

                using DrawingPen outlinePen = new DrawingPen(System.Drawing.Color.FromArgb(100, 0, 0, 0), 2f);
                g.DrawPath(outlinePen, path);

                using var textBrush = new SolidBrush(mainColor);
                g.FillPath(textBrush, path);

                if (!red)
                {
                    var highlightRect = new RectangleF(x, y, textSize.Width, textSize.Height / 2);
                    using var highlightBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                        highlightRect,
                        System.Drawing.Color.FromArgb(40, 255, 255, 255),
                        System.Drawing.Color.FromArgb(0, 255, 255, 255),
                        LinearGradientMode.Vertical);

                    g.FillPath(highlightBrush, path);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            return System.Drawing.Icon.FromHandle(hIcon);
        }

        #endregion

        #region Hotkeys

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Навигация по датам (только если не фокус на ListBox)
            if (e.Key == Key.Left && !IsPresetListFocused())
            {
                ChangeViewedDay(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right && !IsPresetListFocused())
            {
                ChangeViewedDay(1);
                e.Handled = true;
                return;
            }

            // Навигация по пресетам
            if (e.Key == Key.Up && !IsPresetListFocused())
            {
                ChangePreset(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down && !IsPresetListFocused())
            {
                ChangePreset(1);
                e.Handled = true;
                return;
            }

            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

            if (!ctrl || !alt)
                return;

            if (e.Key == Key.D)
            {
                // Переключение старт/пауза
                if (_isRunning)
                {
                    PauseTimer();
                }
                else
                {
                    StartTimer();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.S)
            {
                // Полная остановка и сброс таймера
                StopTimer();
                e.Handled = true;
            }
        }

        private bool IsPresetListFocused()
        {
            return PresetList.IsFocused || PresetList.IsKeyboardFocusWithin;
        }

        private void ChangePreset(int direction)
        {
            if (_presets.Count == 0)
                return;

            int currentIndex = _presets.IndexOf(_currentPreset);
            if (currentIndex == -1)
                currentIndex = 0;

            int newIndex = currentIndex + direction;

            // Циклическая навигация
            if (newIndex < 0)
                newIndex = _presets.Count - 1;
            else if (newIndex >= _presets.Count)
                newIndex = 0;

            _currentPreset = _presets[newIndex];
            PresetList.SelectedItem = _currentPreset;
            PresetList.ScrollIntoView(_currentPreset);
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_pinWindowWhenIdle && !HasActivePeriod && WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                ApplyIdlePinning();
            }
        }

        private void ApplyIdlePinning()
        {
            if (_pinWindowWhenIdle && !HasActivePeriod)
            {
                if (!IsVisible)
                    Show();

                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;

                Topmost = true;
                Activate();
            }
            else if (!_pinWindowWhenIdle && Topmost)
            {
                Topmost = false;
            }
        }

        private void PinWindowWhenIdleCheck_OnChanged(object sender, RoutedEventArgs e)
        {
            _pinWindowWhenIdle = PinWindowWhenIdleCheck?.IsChecked == true;
            _windowSettings.PinWindowWhenIdle = _pinWindowWhenIdle;
            ConfigService.SaveWindowSettings(_windowSettings);
            ApplyIdlePinning();
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
                // Освобождаем иконку перед закрытием
                if (_currentTrayIcon != null)
                {
                    _currentTrayIcon.Dispose();
                    _currentTrayIcon = null;
                }
                base.OnClosing(e);
            }
        }

        #endregion
    }
}