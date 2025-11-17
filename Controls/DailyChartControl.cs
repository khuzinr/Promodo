using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PomodoroTimer.Models;

namespace PomodoroTimer.Controls;

public class DailyChartControl : FrameworkElement
{
    public List<PomodoroStatsEntry> Entries
    {
        get => (List<PomodoroStatsEntry>)GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    public static readonly DependencyProperty EntriesProperty =
        DependencyProperty.Register(nameof(Entries), typeof(List<PomodoroStatsEntry>),
            typeof(DailyChartControl),
            new FrameworkPropertyMetadata(new List<PomodoroStatsEntry>(), FrameworkPropertyMetadataOptions.AffectsRender));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)), null, rect);

        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        double marginLeft = 40;
        double marginBottom = 25;
        double marginTop = 10;
        double marginRight = 10;

        double w = ActualWidth - marginLeft - marginRight;
        double h = ActualHeight - marginTop - marginBottom;
        if (w <= 0 || h <= 0) return;

        double ox = marginLeft;
        double oy = ActualHeight - marginBottom;

        var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), 1);
        // оси
        dc.DrawLine(axisPen, new Point(ox, oy), new Point(ox + w, oy));
        dc.DrawLine(axisPen, new Point(ox, oy), new Point(ox, oy - h));

        // подписи по X
        foreach (var hour in new[] { 0, 6, 12, 18, 24 })
        {
            double x = ox + w * (hour / 24.0);
            dc.DrawLine(axisPen, new Point(x, oy), new Point(x, oy + 3));

            var text = new FormattedText(
                hour.ToString(),
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                Brushes.Gray,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(text, new Point(x - text.Width / 2, oy + 5));
        }

        // подпись Y
        var yText = new FormattedText(
            "Минуты",
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            10,
            Brushes.Gray,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.PushTransform(new RotateTransform(-90, 10, oy - h / 2));
        dc.DrawText(yText, new Point(10 - yText.Width / 2, oy - h / 2 - yText.Height / 2));
        dc.Pop();

        if (Entries == null || Entries.Count == 0)
            return;

        var maxDuration = Entries.Max(e => e.DurationMinutes);
        if (maxDuration <= 0) maxDuration = 1;

        // минимальная ширина столбика в пикселях, чтобы не исчезали совсем
        double minBarWidthPx = 6.0;

        var workBrush = new SolidColorBrush(Color.FromRgb(90, 200, 90));
        var restBrush = new SolidColorBrush(Color.FromRgb(80, 140, 255));
        if (workBrush.CanFreeze) workBrush.Freeze();
        if (restBrush.CanFreeze) restBrush.Freeze();

        foreach (var entry in Entries)
        {
            // старт и конец интервала по времени суток
            double start = entry.TimeMinutes;
            double end = entry.TimeMinutes + entry.DurationMinutes;

            if (start < 0) start = 0;
            if (end > 24 * 60) end = 24 * 60;

            double startNorm = start / (24 * 60.0);
            double endNorm = end / (24 * 60.0);

            double xLeft = ox + w * startNorm;
            double xRight = ox + w * endNorm;
            double width = xRight - xLeft;

            if (width < minBarWidthPx)
            {
                // гарантируем минимальную видимую ширину
                width = minBarWidthPx;
                xRight = xLeft + width;
                if (xRight > ox + w)
                {
                    xRight = ox + w;
                    xLeft = xRight - width;
                }
            }

            // высота ∝ длительности
            double hNorm = entry.DurationMinutes / maxDuration;
            double barHeight = h * hNorm;
            double yTop = oy - barHeight;

            var barRect = new Rect(
                xLeft,
                yTop,
                xRight - xLeft,
                barHeight);

            var brush = string.Equals(entry.Type, "rest", StringComparison.OrdinalIgnoreCase)
                ? restBrush
                : workBrush;

            dc.DrawRectangle(brush, null, barRect);
        }

    }
}
