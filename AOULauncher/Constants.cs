using System;
using System.IO;
using System.Reflection;

namespace AOULauncher;

public static class Constants
{
    public const string ApiLocation = "https://www.xtracube.dev/assets/js/launcherData.json";
    
    public static readonly string DataLocation = Path.Combine(AppContext.BaseDirectory, "LauncherData");
    public static readonly string ModFolder = Path.Combine(DataLocation, "Modpack");
    public static readonly string ConfigPath = Path.Combine(DataLocation, "launcherConfig.json");

    public static readonly string[] UninstallPaths = ["doorstop_config.ini","winhttp.dll"];
}