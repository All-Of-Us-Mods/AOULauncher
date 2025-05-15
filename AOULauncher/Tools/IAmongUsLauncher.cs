using System;

namespace AOULauncher.Tools;

public interface IAmongUsLauncher
{
    public void Launch(Action onExit, params string[] args);
}