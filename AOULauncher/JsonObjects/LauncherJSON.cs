﻿
namespace AOULauncher;

public struct LauncherConfig()
{
    public string AmongUsPath { get; set; } = "";
    public AmongUsPlatform Platform { get; set; } = AmongUsPlatform.Steam;
    public ModPackData ModPackData { get; set; } = default;
}

public struct ModPackData()
{
    public ZipData BepInEx { get; set; } = default;

    public ZipData ExtraData { get; set; } = default;

    public ModInfo[] ModList { get; set; } = [];

    public struct ZipData()
    {
        public string Link { get; set; } = "";

        public string Hash { get; set; } = "";
    }
    
    public struct ModInfo()
    {
        public string Name { get; set; } = "";

        public string Hash { get; set; } = "";

        public string Download { get; set; } = "";
    }
}