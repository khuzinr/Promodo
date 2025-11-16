namespace PomodoroTimer.Models;

public class PomodoroStatsEntry
{
    public double TimeMinutes { get; set; }
    public double DurationMinutes { get; set; }
    public string Type { get; set; } = "work";
}
