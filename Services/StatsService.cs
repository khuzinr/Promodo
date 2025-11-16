using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PomodoroTimer.Models;

namespace PomodoroTimer.Services;

public static class StatsService
{
    private static string StatsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PomodoroTimer");

    private static string StatsPath => Path.Combine(StatsDir, "stats.json");

    public static Dictionary<string, List<PomodoroStatsEntry>> LoadStats()
    {
        try
        {
            if (!File.Exists(StatsPath))
                return new();

            var json = File.ReadAllText(StatsPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<PomodoroStatsEntry>>>(json);
            return data ?? new();
        }
        catch
        {
            return new();
        }
    }

    public static void SaveStats(Dictionary<string, List<PomodoroStatsEntry>> stats)
    {
        try
        {
            Directory.CreateDirectory(StatsDir);
            var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StatsPath, json);
        }
        catch
        {
        }
    }
}
