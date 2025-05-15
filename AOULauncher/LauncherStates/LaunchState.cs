using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using AOULauncher.Tools;
using AOULauncher.Views;

namespace AOULauncher.LauncherStates;

public class LaunchState(MainWindow window) : AbstractLauncherState(window)
{
    public override async Task ButtonClick()
    {
        Utilities.KillAmongUs();

        // make sure bepinex is set to match platform:
        ZipArchive zipFile;
        if (AmongUsLocator.Is64Bit(Path.Combine(Window.Config.AmongUsPath, "Among Us.exe")))
        {
            zipFile = await Window.HttpClient.DownloadZip("BepInEx64.zip", Constants.DataLocation, Window.Config.ModPackData.BepInEx64);
        }
        else
        {
            zipFile = await Window.HttpClient.DownloadZip("BepInEx32.zip", Constants.DataLocation, Window.Config.ModPackData.BepInEx);
        }

        zipFile.ExtractToDirectory(Constants.ModFolder, true);
 
        // copy doorstop
        CopyFromModToGame("winhttp.dll");

        Window.LauncherState = new RunningState(Window);

        var cheater = new FileInfo(Path.GetFullPath("version.dll", Config.AmongUsPath));
        if (cheater.Exists)
        {
            cheater.MoveTo(Path.ChangeExtension(cheater.FullName, ".dll.no"));
        }

        var launcher = AmongUsLocator.GetLauncher(Config.AmongUsPath);

        if (launcher is null)
        {
            Window.LoadAmongUsPath();
            return;
        }

        await SetDoorstopConfig();

        var targetAssembly = Path.Combine(Constants.ModFolder, "BepInEx", "core", "BepInEx.Unity.IL2CPP.dll");
        var coreclrDir = Path.Combine(Constants.ModFolder, "dotnet");
        var coreclrPath = Path.Combine(Constants.ModFolder, "dotnet", "coreclr.dll");

        string[] arguments = [
            "--doorstop-enabled true", 
            $"--doorstop-target-assembly \"{targetAssembly}\"",
            $"--doorstop-clr-corlib-dir \"{coreclrDir}\"",
            $"--doorstop-clr-runtime-coreclr-path \"{coreclrPath}\""
        ];

        launcher.Launch(Window.AmongUsOnExit, arguments);
        Window.LaunchWarning.IsVisible = true;
    }

    public override void EnterState()
    {
        Window.InstallText.Text = "Launch";
        Window.InstallButton.IsEnabled = true;
        Window.SetInfoToPath();
    }

    // create our own doorstop config
    private async Task SetDoorstopConfig()
    {
        var targetAssembly = Path.Combine(Constants.ModFolder, "BepInEx", "core", "BepInEx.Unity.IL2CPP.dll");
        var coreclrDir = Path.Combine(Constants.ModFolder, "dotnet");
        var coreclrPath = Path.Combine(Constants.ModFolder, "dotnet", "coreclr.dll");

        var rawCfg = $"""
                      [General]
                      enabled = true
                      target_assembly = {targetAssembly}
                      [Il2Cpp]
                      coreclr_path = {coreclrPath}
                      corlib_dir = {coreclrDir}
                      """;
        var existingCfg = new FileInfo(Path.Combine(Config.AmongUsPath, "doorstop_config.ini"));
        var existingBak = new FileInfo(Path.Combine(Config.AmongUsPath, "doorstop_config.ini.bak"));
        if (existingCfg.Exists && !existingBak.Exists)
        {
            existingCfg.MoveTo(Path.ChangeExtension(existingCfg.FullName, ".ini.bak"));
        }
        
        await File.WriteAllTextAsync(Path.Combine(Config.AmongUsPath, "doorstop_config.ini"), rawCfg);
    }

    private void CopyFromModToGame(string path)
    {
        File.Copy(Path.Combine(Constants.ModFolder, path), Path.Combine(Config.AmongUsPath, path), true);
    }
}