using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PomodoroTimer.Models;

namespace PomodoroTimer;

public partial class StatsWindow : Window
{
    private readonly Dictionary<string, List<PomodoroStatsEntry>> _stats;
    private readonly List<DayCell> _dayCells = new();
    private readonly HashSet<DateTime> _selectedDates = new();
    private DateTime _visibleMonth;
    private bool _isDragging;
    private DayCell? _dragStart;
    private string? _dragMode; // "row" or "column"

    private readonly Brush _cellBackground;
    private readonly Brush _cellBackgroundSelected;
    private readonly Brush _cellBorder;
    private readonly Brush _cellTodayBorder;

    public StatsWindow(Dictionary<string, List<PomodoroStatsEntry>> stats)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => TryApplyDarkTitleBar();

        _cellBackground = ResolveBrush("CardBrush", Color.FromRgb(0x22, 0x22, 0x22));
        _cellBorder = ResolveBrush("BorderBrushColor", Color.FromRgb(0x33, 0x33, 0x33));
        _cellTodayBorder = ResolveBrush("AccentBrush", Color.FromRgb(0x4C, 0xAF, 0x50));
        _cellBackgroundSelected = CreateSelectionBrush();

        _stats = stats;
        _visibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        BuildCalendarCells();
        RenderMonth();
        SelectInitialWeek();
        UpdateChart();

        MouseLeftButtonUp += StatsWindow_MouseLeftButtonUp;
        MouseMove += StatsWindow_MouseMove;
    }

    public void RefreshData()
    {
        UpdateChart();
    }

    private void StatsWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        _dragStart = null;
        _dragMode = null;
        ReleaseMouseCapture();
    }

    private void BuildCalendarCells()
    {
        CalendarUniformGrid.Children.Clear();
        _dayCells.Clear();

        for (int i = 0; i < 42; i++)
        {
            var border = new Border
            {
                Margin = new Thickness(2),
                Padding = new Thickness(4),
                Background = _cellBackground,
                BorderBrush = _cellBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand
            };

            var text = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16
            };

            border.Child = text;
            CalendarUniformGrid.Children.Add(border);

            var cell = new DayCell
            {
                Border = border,
                Label = text,
                Row = i / 7,
                Column = i % 7
            };

            border.MouseLeftButtonDown += DayCell_MouseLeftButtonDown;
            border.MouseEnter += DayCell_MouseEnter;

            _dayCells.Add(cell);
        }
    }

    private void DayCell_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _dragStart == null || sender is not Border border)
            return;

        var cell = _dayCells.FirstOrDefault(c => c.Border == border);
        if (cell == null || cell == _dragStart)
            return;

        ApplyDragSelection(cell);
    }

    private void RenderMonth()
    {
        MonthText.Text = _visibleMonth.ToString("MMMM yyyy", CultureInfo.CurrentUICulture);

        var firstDay = new DateTime(_visibleMonth.Year, _visibleMonth.Month, 1);
        int offset = GetDayOfWeekIndex(firstDay.DayOfWeek);
        var start = firstDay.AddDays(-offset);

        for (int i = 0; i < _dayCells.Count; i++)
        {
            var cell = _dayCells[i];
            var date = start.AddDays(i);
            cell.Date = date;
            cell.IsCurrentMonth = date.Month == _visibleMonth.Month;
            cell.Label.Text = date.Day.ToString();
            cell.Label.Opacity = cell.IsCurrentMonth ? 1.0 : 0.45;
            UpdateCellVisual(cell);
        }
    }

    private void SelectInitialWeek()
    {
        DayCell? target = _dayCells.FirstOrDefault(c => c.Date.Date == DateTime.Today.Date)
            ?? _dayCells.FirstOrDefault(c => c.IsCurrentMonth);

        if (target == null)
            return;

        var rowDates = _dayCells
            .Where(c => c.Row == target.Row)
            .Select(c => c.Date.Date)
            .ToList();

        SetSelection(rowDates);
    }

    private void DayCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border)
            return;

        var cell = _dayCells.FirstOrDefault(c => c.Border == border);
        if (cell == null)
            return;

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            ToggleDateSelection(cell.Date.Date);
            e.Handled = true;
            return;
        }

        _isDragging = true;
        _dragStart = cell;
        _dragMode = null;
        CaptureMouse();

        SetSelection(new[] { cell.Date.Date });
        e.Handled = true;
    }

    private void StatsWindow_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging || _dragStart == null)
            return;

        var cell = FindCellUnderPointer();
        if (cell == null || cell == _dragStart)
            return;

        ApplyDragSelection(cell);
    }

    private void ApplyDragSelection(DayCell cell)
    {
        _dragMode ??= DetermineDragMode(cell);

        if (_dragMode == "row")
        {
            int startCol = Math.Min(_dragStart!.Column, cell.Column);
            int endCol = Math.Max(_dragStart.Column, cell.Column);
            var dates = _dayCells
                .Where(c => c.Row == _dragStart.Row && c.Column >= startCol && c.Column <= endCol)
                .Select(c => c.Date.Date);
            SetSelection(dates);
        }
        else if (_dragMode == "column")
        {
            int startRow = Math.Min(_dragStart!.Row, cell.Row);
            int endRow = Math.Max(_dragStart.Row, cell.Row);
            var dates = _dayCells
                .Where(c => c.Column == _dragStart.Column && c.Row >= startRow && c.Row <= endRow)
                .Select(c => c.Date.Date);
            SetSelection(dates);
        }
    }

    private string DetermineDragMode(DayCell cell)
    {
        if (_dragStart == null)
            return "row";

        if (cell.Row == _dragStart.Row)
            return "row";
        if (cell.Column == _dragStart.Column)
            return "column";

        int rowDelta = Math.Abs(cell.Row - _dragStart.Row);
        int columnDelta = Math.Abs(cell.Column - _dragStart.Column);
        return columnDelta >= rowDelta ? "row" : "column";
    }

    private DayCell? FindCellUnderPointer()
    {
        var point = Mouse.GetPosition(CalendarUniformGrid);
        if (!IsPointInsideCalendar(point))
            return null;

        DependencyObject? element = CalendarUniformGrid.InputHitTest(point) as DependencyObject;
        while (element != null)
        {
            if (element is Border border)
                return _dayCells.FirstOrDefault(c => c.Border == border);

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private bool IsPointInsideCalendar(Point point)
    {
        return point.X >= 0 && point.Y >= 0 && point.X <= CalendarUniformGrid.ActualWidth && point.Y <= CalendarUniformGrid.ActualHeight;
    }

    private void SetSelection(IEnumerable<DateTime> dates)
    {
        _selectedDates.Clear();
        foreach (var date in dates)
        {
            _selectedDates.Add(date.Date);
        }

        UpdateAllCellVisuals();
        UpdateChart();
    }

    private void ToggleDateSelection(DateTime date)
    {
        var normalized = date.Date;
        if (_selectedDates.Contains(normalized))
        {
            _selectedDates.Remove(normalized);
        }
        else
        {
            _selectedDates.Add(normalized);
        }

        UpdateAllCellVisuals();
        UpdateChart();
    }

    private void UpdateAllCellVisuals()
    {
        foreach (var cell in _dayCells)
        {
            UpdateCellVisual(cell);
        }
    }

    private void UpdateCellVisual(DayCell cell)
    {
        bool isSelected = _selectedDates.Contains(cell.Date.Date);
        bool isToday = cell.Date.Date == DateTime.Today;

        cell.Border.Background = isSelected ? _cellBackgroundSelected : _cellBackground;
        cell.Border.BorderBrush = isToday ? _cellTodayBorder : _cellBorder;
        cell.Border.BorderThickness = isToday ? new Thickness(2) : new Thickness(1);
    }

    private void UpdateChart()
    {
        if (_selectedDates.Count == 0)
        {
            SelectionInfoText.Text = "Выберите несколько дней на календаре справа.";
            MultiDayChart.Summaries = new List<PomodoroDaySummary>();
            TypeBreakdownList.ItemsSource = Array.Empty<TypeBreakdown>();
            return;
        }

        var orderedDates = _selectedDates
            .OrderBy(d => d)
            .ToList();

        var summaries = orderedDates
            .Select(CreateDaySummary)
            .ToList();

        MultiDayChart.Summaries = summaries;
        UpdateTypeBreakdown(orderedDates);

        var first = orderedDates.First();
        var last = orderedDates.Last();
        var culture = CultureInfo.CurrentUICulture;
        string range = first == last
            ? first.ToString("dd MMMM yyyy", culture)
            : $"{first:dd MMM} — {last:dd MMM yyyy}";
        SelectionInfoText.Text = $"Выбрано {orderedDates.Count} дн. ({range}).";
    }

    private PomodoroDaySummary CreateDaySummary(DateTime date)
    {
        string key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!_stats.TryGetValue(key, out var entries) || entries == null)
            return new PomodoroDaySummary { Date = date, TotalMinutes = 0, Segments = new List<PomodoroDayTypeSegment>() };

        var groups = entries
            .Where(e => e.DurationMinutes > 0)
            .GroupBy(e => NormalizeType(e))
            .Select(g => new PomodoroDayTypeSegment
            {
                Type = g.Key,
                Minutes = g.Sum(e => e.DurationMinutes),
                ColorHex = ResolveColorHex(g)
            })
            .ToList();

        double totalMinutes = groups.Sum(g => g.Minutes);

        return new PomodoroDaySummary
        {
            Date = date,
            TotalMinutes = totalMinutes,
            Segments = groups
        };
    }

    private void UpdateTypeBreakdown(IEnumerable<DateTime> dates)
    {
        var normalizedDates = dates.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToHashSet();

        var allEntries = _stats
            .Where(kvp => normalizedDates.Contains(kvp.Key))
            .SelectMany(kvp => kvp.Value ?? new List<PomodoroStatsEntry>())
            .Where(e => e.DurationMinutes > 0)
            .ToList();

        var breakdown = allEntries
            .GroupBy(e => NormalizeType(e))
            .OrderByDescending(g => g.Sum(e => e.DurationMinutes))
            .Select(g => new TypeBreakdown
            {
                Name = g.Key,
                Minutes = g.Sum(e => e.DurationMinutes),
                ColorHex = ResolveColorHex(g)
            })
            .ToList();

        TypeBreakdownList.ItemsSource = breakdown;
    }

    private static string NormalizeType(PomodoroStatsEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Type))
            return entry.Type.Trim();

        return IsRestEntry(entry) ? "Отдых" : "Работа";
    }

    private static string ResolveColorHex(IEnumerable<PomodoroStatsEntry> entries)
    {
        string? explicitHex = entries
            .Select(e => e.ColorHex)
            .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));

        if (!string.IsNullOrWhiteSpace(explicitHex))
            return explicitHex;

        bool isRest = entries.Any(IsRestEntry);
        return isRest ? "#9B59B6" : "#5AC85A";
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

    private static int GetDayOfWeekIndex(DayOfWeek day)
    {
        int index = (int)day - 1;
        return index < 0 ? 6 : index;
    }

    private Brush ResolveBrush(string key, Color fallback)
    {
        if (TryFindResource(key) is Brush brush)
            return brush;
        if (Application.Current?.TryFindResource(key) is Brush appBrush)
            return appBrush;
        return new SolidColorBrush(fallback);
    }

    private Brush CreateSelectionBrush()
    {
        var accent = ResolveBrush("AccentBrush", Color.FromRgb(0x4C, 0xAF, 0x50));
        if (accent is SolidColorBrush solid)
        {
            var color = solid.Color;
            return new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
        }

        var clone = accent.CloneCurrentValue();
        clone.Opacity = 0.7;
        return clone;
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(-1);
        RenderMonth();
        SelectInitialWeek();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(1);
        RenderMonth();
        SelectInitialWeek();
    }

    private void TryApplyDarkTitleBar()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return;

        var helper = new WindowInteropHelper(this);
        var hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
            return;

        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

        int trueValue = 1;
        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueValue, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref trueValue, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static string FormatDuration(double minutes)
    {
        var totalMinutes = (int)Math.Round(minutes);
        int hours = totalMinutes / 60;
        int mins = totalMinutes % 60;
        if (hours > 0)
            return $"{hours}ч {mins}м";
        return $"{mins}м";
    }

    private class DayCell
    {
        public Border Border { get; init; } = null!;
        public TextBlock Label { get; init; } = null!;
        public int Row { get; init; }
        public int Column { get; init; }
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
    }

    private class TypeBreakdown
    {
        public string Name { get; init; } = string.Empty;
        public double Minutes { get; init; }
        public string ColorHex { get; init; } = "#5AC85A";
        public Color Color => (Color)ColorConverter.ConvertFromString(ColorHex);
        public string MinutesFormatted => FormatDuration(Minutes);
    }
}
