using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AOULauncher.Tools;

public class NormalLauncher(string path) : IAmongUsLauncher
{
    public bool CopyDoorstop => false;

    public void Launch(Action onExit, params string[] args)
    {
        // resolves doorstop without having to copy to game directory
        SetDllDirectory(Constants.ModFolder);
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

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool SetDllDirectory(string lpPathName);
}