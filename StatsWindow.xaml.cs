using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using PomodoroTimer.Models;

namespace PomodoroTimer;

public partial class StatsWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<DailyStatsViewModel> DailyStats { get; } = new();

    private double _maxMinutes = 1;
    public double MaxMinutes
    {
        get => _maxMinutes;
        private set
        {
            if (Math.Abs(_maxMinutes - value) > double.Epsilon)
            {
                _maxMinutes = value;
                OnPropertyChanged();
            }
        }
    }

    public string TodayLabel => DateTime.Now.ToString("dddd, dd MMMM", CultureInfo.CurrentCulture);

    public StatsWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void UpdateData(Dictionary<string, List<PomodoroStatsEntry>> stats)
    {
        DailyStats.Clear();

        var today = DateTime.Today;
        double max = 1;

        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var key = date.ToString("yyyy-MM-dd");
            stats.TryGetValue(key, out var entries);
            entries ??= new List<PomodoroStatsEntry>();

            var orderedEntries = entries.OrderBy(e => e.TimeMinutes).ToList();
            double totalMinutes = orderedEntries.Sum(e => e.DurationMinutes);
            int pomodoros = orderedEntries.Count(e => string.Equals(e.Type, "work", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(e.Type));

            var viewModel = new DailyStatsViewModel
            {
                Date = date,
                DayLabel = date.ToString("dddd, dd MMM", CultureInfo.CurrentCulture),
                Pomodoros = pomodoros,
                TotalMinutes = totalMinutes,
                Tooltip = BuildTooltip(orderedEntries, date)
            };

            DailyStats.Add(viewModel);

            if (totalMinutes > max)
            {
                max = totalMinutes;
            }
        }

        MaxMinutes = Math.Max(1, max);
    }

    private static string BuildTooltip(List<PomodoroStatsEntry> entries, DateTime day)
    {
        if (entries.Count == 0)
        {
            return $"No pomodoros finished on {day:dd MMMM}.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{day:dddd, dd MMMM}");
        sb.AppendLine("Pomodoros:");

        foreach (var entry in entries)
        {
            var start = TimeSpan.FromMinutes(entry.TimeMinutes);
            var duration = TimeSpan.FromMinutes(entry.DurationMinutes);
            var end = start + duration;
            sb.AppendLine($"• {start:hh\\:mm} – {end:hh\\:mm} ({duration.TotalMinutes:0} min)");
        }

        return sb.ToString().TrimEnd();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DailyStatsViewModel
{
    public DateTime Date { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public int Pomodoros { get; set; }
    public double TotalMinutes { get; set; }
    public string Tooltip { get; set; } = string.Empty;
}
