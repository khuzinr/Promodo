using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace PomodoroTimer.Services;

public class CommandServer : IDisposable
{
    private readonly MainWindow _window;
    private readonly CancellationTokenSource _cts = new();

    public CommandServer(MainWindow window)
    {
        _window = window;
        Task.Run(ListenLoop);
    }

    private async Task ListenLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream("PomodoroTimerPipe", PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cts.Token);
                using var sr = new StreamReader(server);
                var cmd = await sr.ReadLineAsync();
                if (cmd != null)
                {
                    HandleCommand(cmd);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // ignore and continue
            }
        }
    }

    public void HandleCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verb = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : "";

        _window.Dispatcher.BeginInvoke(new Action(() =>
        {
            switch (verb)
            {
                case "start":
                    _window.StartTimer();
                    break;
                case "stop":
                    _window.StopTimer();
                    break;
                case "pause":
                    _window.PauseTimer();
                    break;
                case "toggle":
                    _window.ToggleWorkRestMode();
                    break;
                case "rest":
                    if (_window.IsWorking)
                        _window.ToggleWorkRestMode();
                    _window.StartTimer();
                    break;
                case "show":
                    _window.ShowWindow();
                    break;
                case "quit":
                    _window.QuitFromExternal();
                    break;
                case "preset":
                    if (!string.IsNullOrWhiteSpace(arg))
                        _window.SelectPresetByName(arg.Trim().Trim('"'));
                    break;
            }
        }));
    }

    public void Dispose()
    {
        _cts.Cancel();
    }
}
