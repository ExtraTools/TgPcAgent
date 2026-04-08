using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TgPcAgent.App.Services;

public sealed class PowerService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    public Task LockAsync()
    {
        if (!LockWorkStation())
        {
            throw new InvalidOperationException("LockWorkStation returned false.");
        }

        return Task.CompletedTask;
    }

    public Task SleepAsync()
    {
        Application.SetSuspendState(PowerState.Suspend, true, true);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "/s /t 0",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        return Task.CompletedTask;
    }

    public Task RestartAsync()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "/r /t 0",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        return Task.CompletedTask;
    }
}
