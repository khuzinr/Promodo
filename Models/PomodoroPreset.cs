namespace PomodoroTimer.Models;

public class PomodoroPreset
{
    public string Name { get; set; } = "Default";
    public int WorkMinutes { get; set; } = 25;
    public int RestMinutes { get; set; } = 5;

    public override string ToString() => Name;
}
