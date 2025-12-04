using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using PomodoroTimer.Models;

namespace PomodoroTimer;

public partial class TimerButtonSettingsWindow : Window
{
    private readonly List<TimerButtonDefinition> _buttons;

    public IReadOnlyList<TimerButtonDefinition> Result => _buttons;

    public TimerButtonSettingsWindow(IEnumerable<TimerButtonDefinition> buttons)
    {
        InitializeComponent();

        _buttons = buttons
            .Select(CloneButton)
            .ToList();

        ButtonsRepeater.ItemsSource = _buttons;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        TryEnableDarkTitleBar();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        NormalizeButtons();
        DialogResult = true;
        Close();
    }

    private void NormalizeButtons()
    {
        var seenRest = false;
        foreach (var button in _buttons)
        {
            if (string.IsNullOrWhiteSpace(button.Id))
                button.Id = Guid.NewGuid().ToString();

            button.Name = string.IsNullOrWhiteSpace(button.Name)
                ? (button.IsRest ? "Отдых" : "Работа")
                : button.Name.Trim();

            if (string.IsNullOrWhiteSpace(button.BackgroundColorHex))
                button.BackgroundColorHex = button.IsRest ? "#9B59B6" : "#4CAF50";

            if (string.IsNullOrWhiteSpace(button.TextColorHex))
                button.TextColorHex = "#FFFFFF";

            if (button.IsRest)
                seenRest = true;
        }

        if (!seenRest)
        {
            _buttons.Add(new TimerButtonDefinition
            {
                Id = "rest",
                Name = "Отдых",
                BackgroundColorHex = "#9B59B6",
                TextColorHex = "#FFFFFF",
                IsRest = true
            });
        }
    }

    private static TimerButtonDefinition CloneButton(TimerButtonDefinition source)
    {
        return new TimerButtonDefinition
        {
            Id = source.Id,
            Name = source.Name,
            BackgroundColorHex = source.BackgroundColorHex,
            TextColorHex = source.TextColorHex,
            IsRest = source.IsRest
        };
    }

    private void TryEnableDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int useDark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int));
            DwmSetWindowAttribute(hwnd, 19, ref useDark, sizeof(int));
        }
        catch
        {
            // ignore
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
