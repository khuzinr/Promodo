using System;
using System.Windows;
using System.Windows.Interop;
using PomodoroTimer.Models;

namespace PomodoroTimer
{
    public partial class PresetWindow : Window
    {
        public PomodoroPreset? Result { get; private set; }

        // 🔹 Конструктор для добавления нового пресета
        public PresetWindow()
        {
            InitializeComponent();
        }

        // 🔹 Конструктор для редактирования существующего пресета
        public PresetWindow(PomodoroPreset preset) : this()
        {
            NameBox.Text = preset.Name;
            WorkBox.Text = preset.WorkMinutes.ToString();
            RestBox.Text = preset.RestMinutes.ToString();
        }

        // 🟦 Включение тёмного title bar как в MainWindow
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            EnableDarkTitleBar();
        }

        private void EnableDarkTitleBar()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int useDark = 1;

                // Windows 11 / Windows 10 21H2+
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

                // Windows 10 старые версии
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));
            }
            catch
            {
                // Игнорируем — просто means not supported
            }
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        // 🟩 Кнопка OK
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(WorkBox.Text, out int work) || work <= 0)
                work = 25;

            if (!int.TryParse(RestBox.Text, out int rest) || rest <= 0)
                rest = 5;

            Result = new PomodoroPreset
            {
                Name = string.IsNullOrWhiteSpace(NameBox.Text)
                    ? "Preset"
                    : NameBox.Text.Trim(),
                WorkMinutes = work,
                RestMinutes = rest
            };

            DialogResult = true;
            Close();
        }
    }
}
