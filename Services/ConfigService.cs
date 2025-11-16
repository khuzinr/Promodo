using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PomodoroTimer.Models;

namespace PomodoroTimer.Services;

public static class ConfigService
{
    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PomodoroTimer");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static List<PomodoroPreset> LoadPresets()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new List<PomodoroPreset>();

            var json = File.ReadAllText(ConfigPath);
            var presets = JsonSerializer.Deserialize<List<PomodoroPreset>>(json);
            if (presets == null) return new List<PomodoroPreset>();

            foreach (var p in presets)
            {
                if (p.WorkMinutes <= 0) p.WorkMinutes = 25;
                if (p.RestMinutes <= 0) p.RestMinutes = 5;
                if (string.IsNullOrWhiteSpace(p.Name)) p.Name = "Preset";
            }

            return presets;
        }
        catch
        {
            return new List<PomodoroPreset>();
        }
    }

    public static void SavePresets(List<PomodoroPreset> presets)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(presets, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
        }
    }
}
