namespace PomodoroTimer.Models;

public class PomodoroStatsEntry
{
    public double TimeMinutes { get; set; }
    public double DurationMinutes { get; set; }
    public string Type { get; set; } = "work";
    public string ColorHex { get; set; } = "#5AC85A";
    public bool IsRest { get; set; }
}
