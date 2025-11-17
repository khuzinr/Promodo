using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PomodoroTimer.Models;

namespace PomodoroTimer;

public partial class MonthlyStatsWindow : Window
{
    private readonly Dictionary<string, List<PomodoroStatsEntry>> _stats;
    private DateTime _selectedMonth;

    public MonthlyStatsWindow(Dictionary<string, List<PomodoroStatsEntry>> stats)
    {
        InitializeComponent();
        _stats = stats;
        _selectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        MonthPicker.SelectedDate = _selectedMonth;
        RefreshData();
    }

    public void RefreshData()
    {
        var rows = BuildStatsForMonth(_selectedMonth).ToList();
        WeekGrid.ItemsSource = rows;

        if (rows.Count == 0)
        {
            SummaryText.Text = "За выбранный месяц нет данных.";
        }
        else
        {
            double totalWork = rows.Sum(r => r.WorkMinutes);
            double totalRest = rows.Sum(r => r.RestMinutes);
            SummaryText.Text =
                $"Всего: работа {FormatMinutes(totalWork)}, отдых {FormatMinutes(totalRest)}.";
        }
    }

    private IEnumerable<WeeklyStatsRow> BuildStatsForMonth(DateTime month)
    {
        var culture = CultureInfo.CurrentCulture;
        var cal = culture.Calendar;
        var weekRule = culture.DateTimeFormat.CalendarWeekRule;
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;

        var data = _stats
            .Select(kvp => new
            {
                Date = DateTime.ParseExact(kvp.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Entries = kvp.Value
            })
            .Where(x => x.Date.Year == month.Year && x.Date.Month == month.Month)
            .GroupBy(x => cal.GetWeekOfYear(x.Date, weekRule, firstDay))
            .OrderBy(g => g.Key);

        foreach (var group in data)
        {
            double work = 0;
            double rest = 0;

            foreach (var day in group)
            {
                foreach (var entry in day.Entries)
                {
                    if (string.Equals(entry.Type, "rest", StringComparison.OrdinalIgnoreCase))
                    {
                        rest += entry.DurationMinutes;
                    }
                    else
                    {
                        work += entry.DurationMinutes;
                    }
                }
            }

            yield return new WeeklyStatsRow
            {
                WeekLabel = $"Неделя {group.Key}",
                WorkMinutes = work,
                RestMinutes = rest
            };
        }
    }

    private void MonthPicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!MonthPicker.SelectedDate.HasValue)
            return;

        var selected = MonthPicker.SelectedDate.Value;
        _selectedMonth = new DateTime(selected.Year, selected.Month, 1);
        RefreshData();
    }

    private static string FormatMinutes(double minutes)
    {
        var ts = TimeSpan.FromMinutes(minutes);
        return $"{(int)ts.TotalHours:D2}ч {ts.Minutes:D2}м";
    }

    private class WeeklyStatsRow
    {
        public string WeekLabel { get; set; } = string.Empty;
        public double WorkMinutes { get; set; }
        public double RestMinutes { get; set; }
        public string WorkDisplay => FormatMinutes(WorkMinutes);
        public string RestDisplay => FormatMinutes(RestMinutes);
        public string TotalDisplay => FormatMinutes(WorkMinutes + RestMinutes);
    }
}
