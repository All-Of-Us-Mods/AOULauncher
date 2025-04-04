﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AOULauncher.Enum;
using Avalonia.Threading;

namespace AOULauncher.Tools;

public class AmongUsLauncher(string amongUsPath, AmongUsPlatform platform, Action onExitCallback, params string[] args)
{
    public void Launch()
    {
        switch (platform)
        {
            case AmongUsPlatform.Steam:
                NormalLaunch();
                //IndirectLaunch("steam://run/945360/");
                break;
            case AmongUsPlatform.Itch:
                NormalLaunch();
                break;
            case AmongUsPlatform.Epic:
                IndirectLaunch("com.epicgames.launcher://apps/33956bcb55d4452d8c47e16b94e294bd%3A729a86a5146640a2ace9e8c595414c56%3A963137e4c29d4c79a81323b8fab03a40?action=launch&silent=true");
                break;
            default:
                NormalLaunch();
                break;
        }
    }

    private void NormalLaunch()
    {
        var psi = new ProcessStartInfo(Path.Combine(amongUsPath, "Among Us.exe"))
        {
            Arguments = string.Join(" ", args)
        };
        
        var process = Process.Start(psi);
        if (process is null)
        {
            return;
        }
        
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => Dispatcher.UIThread.InvokeAsync(onExitCallback);
    }

    private void IndirectLaunch(string path)
    {
        var psi = new ProcessStartInfo(path)
        {
            UseShellExecute = true
        };
        
        Process.Start(psi);
        Task.Run(WaitForAmongUs);
    }

    private async Task WaitForAmongUs()
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
            process.Exited += (_, _) => Dispatcher.UIThread.InvokeAsync(onExitCallback);
            return;
        }
        
        onExitCallback();
    }
}