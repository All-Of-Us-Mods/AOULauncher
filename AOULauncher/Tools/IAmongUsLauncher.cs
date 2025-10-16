using System;

namespace AOULauncher.Tools;

public interface IAmongUsLauncher
{
    public bool CopyDoorstop { get; }
    public void Launch(Action onExit, params string[] args);
}