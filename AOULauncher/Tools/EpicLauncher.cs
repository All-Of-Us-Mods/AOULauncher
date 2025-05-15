using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace AOULauncher.Tools;

public class EpicLauncher : IAmongUsLauncher
{
    public void Launch(Action onExit, params string[] args)
    {
        var psi = new ProcessStartInfo("com.epicgames.launcher://apps/33956bcb55d4452d8c47e16b94e294bd%3A729a86a5146640a2ace9e8c595414c56%3A963137e4c29d4c79a81323b8fab03a40?action=launch&silent=true")
        {
            UseShellExecute = true
        };

        Process.Start(psi);
        Task.Run(() => WaitForAmongUs(onExit));
    }

    private static async Task WaitForAmongUs(Action onExit)
    {
        for (var i = 0; i < 60; i++)
        {
            await Task.Delay(500);

            var processes = Process.GetProcessesByName("Among Us");
            if (processes.Length <= 0)
            {
                continue;
            }
            
            var process = processes[0];
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Dispatcher.UIThread.InvokeAsync(onExit);
            return;
        }

        onExit();
    }
}