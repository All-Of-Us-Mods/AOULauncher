using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using AOULauncher.Enum;
using AOULauncher.Tools;
using AOULauncher.Views;

namespace AOULauncher.LauncherStates;

public class InstallState(MainWindow window) : AbstractLauncherState(window)
{
    public override async Task ButtonClick()
    {
        Window.LauncherState = new LoadingState(Window);
        await InstallMod();
        Window.LoadAmongUsPath();
    }

    public override void EnterState()
    {
        Window.InstallText.Text = "Install";
        Window.InstallButton.IsEnabled = true;
        Window.SetInfoToPath();
    }

    // installs the mod to the ModFolder directory. see the launching logic further down for how doorstop is used
    private async Task InstallMod()
    {
        Utilities.KillAmongUs();

        if (AmongUsLocator.Is64Bit(Path.Combine(Config.AmongUsPath, "Among Us.exe")))
        {
            await InstallZip("BepInEx64.zip", Constants.ModFolder, Config.ModPackData.BepInEx64);
        }
        else
        {
            await InstallZip("BepInEx32.zip", Constants.ModFolder, Config.ModPackData.BepInEx);
        }
        await InstallPlugins(Constants.ModFolder);
        await InstallZip("ExtraData.zip", Constants.ModFolder, Config.ModPackData.ExtraData);
    }
    
    private async Task InstallZip(string name, string directory, ModPackData.ZipData zipData)
    {
        var zipFile = await Window.HttpClient.DownloadZip(name, Constants.DataLocation, zipData);
        
        Window.ProgressBar.ProgressTextFormat = $"Installing {name}";
        zipFile.ExtractToDirectory(directory, true);
        
        Window.ProgressBar.ProgressTextFormat = $"Installed {name}";
    }
    
    private async Task InstallPlugins(string directory)
    {
        var pluginPath = Path.Combine(directory, "BepInEx", "plugins");
        
        Window.ProgressBar.ProgressTextFormat = "Installing mod...";
        Window.ProgressBar.Value = 0;
        
        foreach (var plugin in Config.ModPackData.ModList)
        {
            var path = Path.Combine(pluginPath, plugin.Name);
            if (File.Exists(path) && Utilities.IsPluginUpdated(path, plugin))
            {
                continue;
            }
            Window.ProgressBar.ProgressTextFormat = $"Downloading {plugin.Name}...";

            await Window.HttpClient.DownloadFile(plugin.Name, pluginPath, plugin.Download);
        }
        Window.ProgressBar.ProgressTextFormat = "Installed plugins";
    }
}