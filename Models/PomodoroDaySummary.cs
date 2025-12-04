using System;
using System.Collections.Generic;

namespace PomodoroTimer.Models;

public class PomodoroDaySummary
{
    public DateTime Date { get; set; }
    public double TotalMinutes { get; set; }

    public List<PomodoroDayTypeSegment> Segments { get; set; } = new();
}

public class PomodoroDayTypeSegment
{
    public string Type { get; set; } = string.Empty;
    public double Minutes { get; set; }
    public string ColorHex { get; set; } = "#5AC85A";
}
