using System;
using System.IO;
using System.IO.Pipes;

namespace PomodoroTimer.Services;

public static class CommandClient
{
    public static void SendCommand(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", "PomodoroTimerPipe", PipeDirection.Out);
            client.Connect(500);
            using var sw = new StreamWriter(client);
            sw.AutoFlush = true;
            sw.WriteLine(command);
        }
        catch
        {
            // primary instance not running
        }
    }
}
