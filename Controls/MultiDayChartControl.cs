using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PomodoroTimer.Models;

namespace PomodoroTimer.Controls;

public class MultiDayChartControl : FrameworkElement
{
    public List<PomodoroDaySummary> Summaries
    {
        get => (List<PomodoroDaySummary>)GetValue(SummariesProperty);
        set => SetValue(SummariesProperty, value);
    }

    public static readonly DependencyProperty SummariesProperty =
        DependencyProperty.Register(nameof(Summaries), typeof(List<PomodoroDaySummary>), typeof(MultiDayChartControl),
            new FrameworkPropertyMetadata(new List<PomodoroDaySummary>(), FrameworkPropertyMetadataOptions.AffectsRender));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        var background = ResolveBrush("CardBrush", new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)));
        dc.DrawRectangle(background, null, rect);

        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        double marginLeft = 50;
        double marginBottom = 40;
        double marginTop = 20;
        double marginRight = 20;

        double w = ActualWidth - marginLeft - marginRight;
        double h = ActualHeight - marginTop - marginBottom;
        if (w <= 0 || h <= 0)
            return;

        double ox = marginLeft;
        double oy = ActualHeight - marginBottom;

        var axisPen = new Pen(ResolveBrush("BorderBrushColor", new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))), 1);
        dc.DrawLine(axisPen, new Point(ox, oy), new Point(ox + w, oy));
        dc.DrawLine(axisPen, new Point(ox, oy), new Point(ox, oy - h));

        var summaries = Summaries ?? new List<PomodoroDaySummary>();
        if (summaries.Count == 0)
        {
            DrawCenteredText(dc, "Нет выбранных дней", new Point(ActualWidth / 2, ActualHeight / 2));
            return;
        }

        double maxMinutes = summaries
            .Select(s => Math.Max(s.TotalMinutes, s.Segments?.Sum(seg => seg.Minutes) ?? 0))
            .DefaultIfEmpty(0)
            .Max();
        if (maxMinutes <= 0)
        {
            DrawCenteredText(dc, "Нет данных", new Point(ActualWidth / 2, ActualHeight / 2));
            return;
        }

        int count = summaries.Count;
        double slotWidth = w / count;
        double barWidth = Math.Max(Math.Min(slotWidth * 0.6, slotWidth - 6), 18);
        double barSpacing = (slotWidth - barWidth) / 2;

        var accent = ResolveBrush("AccentBrush", new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)));
        var textBrush = ResolveBrush("TextBrush", Brushes.White);
        var subtleBrush = ResolveBrush("SubtleTextBrush", new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)));
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var fontFamily = new FontFamily("Segoe UI");

        for (int i = 0; i < count; i++)
        {
            var summary = summaries[i];
            double totalMinutes = Math.Max(summary.TotalMinutes, summary.Segments?.Sum(seg => seg.Minutes) ?? 0);
            double normalized = totalMinutes / maxMinutes;
            double barHeight = h * normalized;
            double x = ox + i * slotWidth + barSpacing;
            double yTop = oy - barHeight;

            double currentTop = yTop;
            var segments = summary.Segments ?? new List<PomodoroDayTypeSegment>();
            if (segments.Count == 0)
            {
                var barRect = new Rect(x, yTop, barWidth, barHeight);
                dc.DrawRoundedRectangle(accent, null, barRect, 4, 4);
            }
            else
            {
                foreach (var segment in segments.OrderBy(s => s.Type))
                {
                    double ratio = totalMinutes <= 0 ? 0 : segment.Minutes / totalMinutes;
                    double segmentHeight = barHeight * ratio;
                    var segmentRect = new Rect(x, currentTop, barWidth, segmentHeight);
                    dc.DrawRectangle(CreateBrush(segment.ColorHex, accent), null, segmentRect);
                    currentTop += segmentHeight;
                }
            }

            // value text
            var valueText = FormatDuration(totalMinutes);
            var valueFormatted = new FormattedText(valueText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(fontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 12, textBrush, dpi);
            dc.DrawText(valueFormatted, new Point(x + (barWidth - valueFormatted.Width) / 2, Math.Max(yTop - valueFormatted.Height - 4, marginTop)));

            // date label
            var label = summary.Date.ToString("dd MMM", CultureInfo.CurrentUICulture);
            var labelFormatted = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 11, subtleBrush, dpi);
            dc.DrawText(labelFormatted, new Point(x + (barWidth - labelFormatted.Width) / 2, oy + 6));
        }

        var yLabel = new FormattedText("Минуты", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 10, subtleBrush, dpi);
        dc.PushTransform(new RotateTransform(-90, 12, oy - h / 2));
        dc.DrawText(yLabel, new Point(12 - yLabel.Width / 2, oy - h / 2 - yLabel.Height / 2));
        dc.Pop();
    }

    private static string FormatDuration(double minutes)
    {
        var totalMinutes = (int)Math.Round(minutes);
        int hours = totalMinutes / 60;
        int mins = totalMinutes % 60;
        if (hours > 0)
            return $"{hours}ч {mins}м";
        return $"{mins}м";
    }

    private void DrawCenteredText(DrawingContext dc, string text, Point center)
    {
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var fontFamily = new FontFamily("Segoe UI");
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 14,
            ResolveBrush("SubtleTextBrush", Brushes.Gray), dpi);
        dc.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private static Brush ResolveBrush(string key, Brush fallback)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
            return brush;

        return fallback;
    }

    private static Brush CreateBrush(string? colorHex, Brush fallback)
    {
        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                return new SolidColorBrush(color);
            }
            catch
            {
            }
        }

        return fallback;
    }
}
