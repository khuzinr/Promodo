namespace PomodoroTimer.Models;

public class TimerButtonDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BackgroundColorHex { get; set; } = "#4CAF50";
    public string TextColorHex { get; set; } = "#FFFFFF";
    public bool IsRest { get; set; }
}
