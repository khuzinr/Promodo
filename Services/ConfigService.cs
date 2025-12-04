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
    private static string ButtonsPath => Path.Combine(ConfigDir, "timer-buttons.json");

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

    public static List<TimerButtonDefinition> LoadTimerButtons()
    {
        try
        {
            if (!File.Exists(ButtonsPath))
                return CreateDefaultButtons();

            var json = File.ReadAllText(ButtonsPath);
            var buttons = JsonSerializer.Deserialize<List<TimerButtonDefinition>>(json);
            if (buttons == null || buttons.Count == 0)
                return CreateDefaultButtons();

            EnsureButtonDefaults(buttons);
            return buttons;
        }
        catch
        {
            return CreateDefaultButtons();
        }
    }

    public static void SaveTimerButtons(List<TimerButtonDefinition> buttons)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(buttons, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ButtonsPath, json);
        }
        catch
        {
        }
    }

    private static List<TimerButtonDefinition> CreateDefaultButtons()
    {
        return new List<TimerButtonDefinition>
        {
            new() { Id = "work-1", Name = "Работа 1", BackgroundColorHex = "#E74C3C", TextColorHex = "#FFFFFF" },
            new() { Id = "work-2", Name = "Работа 2", BackgroundColorHex = "#E67E22", TextColorHex = "#1A1A1A" },
            new() { Id = "work-3", Name = "Работа 3", BackgroundColorHex = "#F1C40F", TextColorHex = "#1A1A1A" },
            new() { Id = "work-4", Name = "Работа 4", BackgroundColorHex = "#2ECC71", TextColorHex = "#FFFFFF" },
            new() { Id = "work-5", Name = "Работа 5", BackgroundColorHex = "#1ABC9C", TextColorHex = "#FFFFFF" },
            new() { Id = "rest", Name = "Отдых", BackgroundColorHex = "#9B59B6", TextColorHex = "#FFFFFF", IsRest = true }
        };
    }

    private static void EnsureButtonDefaults(List<TimerButtonDefinition> buttons)
    {
        if (!buttons.Any(b => b.IsRest))
        {
            buttons.Add(new TimerButtonDefinition
            {
                Id = "rest",
                Name = "Отдых",
                BackgroundColorHex = "#9B59B6",
                TextColorHex = "#FFFFFF",
                IsRest = true
            });
        }

        foreach (var button in buttons)
        {
            if (string.IsNullOrWhiteSpace(button.Id))
            {
                button.Id = Guid.NewGuid().ToString();
            }

            if (string.IsNullOrWhiteSpace(button.Name))
            {
                button.Name = button.IsRest ? "Отдых" : "Работа";
            }

            if (string.IsNullOrWhiteSpace(button.BackgroundColorHex))
            {
                button.BackgroundColorHex = button.IsRest ? "#9B59B6" : "#4CAF50";
            }

            if (string.IsNullOrWhiteSpace(button.TextColorHex))
            {
                button.TextColorHex = "#FFFFFF";
            }
        }
    }
}
