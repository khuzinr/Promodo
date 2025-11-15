using System;
using System.Threading;
using System.Windows;
using PomodoroTimer.Services;

namespace PomodoroTimer;

public partial class App : Application
{
    private static Mutex? _mutex;
    private CommandServer? _commandServer;
    private MainWindow? _mainWindow;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        bool createdNew;
        _mutex = new Mutex(true, "PomodoroTimerSingletonMutex", out createdNew);

        if (!createdNew)
        {
            if (e.Args.Length > 0)
            {
                var cmd = string.Join(' ', e.Args);
                CommandClient.SendCommand(cmd);
            }
            Shutdown();
            return;
        }

        _mainWindow = new MainWindow();
        _mainWindow.Show();

        _commandServer = new CommandServer(_mainWindow);

        if (e.Args.Length > 0)
        {
            var cmd = string.Join(' ', e.Args);
            _commandServer.HandleCommand(cmd);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _commandServer?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
