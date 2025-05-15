using System;
using System.Diagnostics;

namespace AOULauncher.Tools;

public class NormalLauncher(string path) : IAmongUsLauncher
{
    public void Launch(Action onExit, params string[] args)
    {
        var psi = new ProcessStartInfo(path)
        {
            Arguments = string.Join(" ", args)
        };
        var process = Process.Start(psi);
        if (process is null)
        {
            return;
        }
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => onExit();
    }
}