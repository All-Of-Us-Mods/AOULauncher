using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using AOULauncher.Enum;
using AOULauncher.LauncherStates;
using AOULauncher.Tools;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AOULauncher.Views;

public partial class MainWindow : Window
{
    public HttpClient HttpClient { get; }

    private AbstractLauncherState _launcherState;

    public AbstractLauncherState LauncherState
    {
        get => _launcherState;
        set
        {
            _launcherState = value;
            _launcherState.EnterState();
        }
    }

    public LauncherConfig Config;

    public MainWindow()
    {
        InitializeComponent();
        // Load config
        Config = File.Exists(Constants.ConfigPath)
            ? JsonSerializer.Deserialize(File.ReadAllText(Constants.ConfigPath), LauncherConfigContext.Default.LauncherConfig)
            : new LauncherConfig();
        
        var progressHandler = new ProgressMessageHandler(new HttpClientHandler {AllowAutoRedirect = true});
        progressHandler.HttpReceiveProgress += (_, args) => {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressBar.Value = args.ProgressPercentage;
            });
        };

        HttpClient = new HttpClient(progressHandler, true);        
        RemoveOutdatedLauncher();

        _launcherState = new InstallState(this);
        _launcherState.EnterState();
        // start downloading launcher data and load among us path to check for mod installation
        Task.Run(DownloadData);
    }
    
    private async Task DownloadData()
    {
        try
        {
            Config.ModPackData = await HttpClient.DownloadJson(Constants.ApiLocation, LauncherConfigContext.Default.ModPackData);
            await CheckLauncherUpdate();
            await Dispatcher.UIThread.InvokeAsync(LoadAmongUsPath);
        }
        catch (Exception e)
        {
            var window = new Error(e.Message);
            window.Show();
            window.Activate();
        }
    }

    private static void RemoveOutdatedLauncher()
    {
        var outdated = new FileInfo(Path.Combine(AppContext.BaseDirectory,"AOULauncher.exe.old"));

        if (outdated.Exists)
        {
            outdated.Delete();
        }
    }
    
    private async Task CheckLauncherUpdate()
    {
        if (!Version.TryParse(Config.ModPackData.LatestLauncherVersion, out var version))
        {
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        await Console.Out.WriteLineAsync(assembly.GetName().Version +", "+version); 
        if (version <= assembly.GetName().Version)
        {
            return;
        }

        var file = new FileInfo(Path.Combine(AppContext.BaseDirectory,"AOULauncher.exe"));

        if (!file.Exists)
        {
            return;
        }

        file.MoveTo(file.FullName+".old", true);

        await HttpClient.DownloadFile("AOULauncher.zip", AppContext.BaseDirectory, Config.ModPackData.LauncherUpdateLink);
        var zipFile = ZipFile.OpenRead("AOULauncher.zip");
        zipFile.ExtractToDirectory(AppContext.BaseDirectory, true);

        Process.Start(Path.Combine(AppContext.BaseDirectory, "AOULauncher.exe"));
        Process.GetCurrentProcess().Kill();
    }

    public void LoadAmongUsPath()
    {
        Console.Out.WriteLine("Loading Among Us Path");
        ProgressBar.Value = 0;
        ProgressBar.ProgressTextFormat = "Loading...";
        
        if (!AmongUsLocator.VerifyAmongUsDirectory(Config.AmongUsPath))
        {
            if (AmongUsLocator.FindAmongUs() is { } path)
            {
                Config.AmongUsPath = path;
            }
            else
            {
                Console.Out.WriteLine("no among us detected");
                LauncherState = new RefreshState(this);
                return;
            }
        }

        Config.Platform = AmongUsLocator.GetPlatform(Config.AmongUsPath, Config.ModPackData) ?? AmongUsPlatform.Unknown;
        if (Config.Platform is AmongUsPlatform.Unknown)
        { 
            SetLaunchWarning("Unknown platform/game version detected!\nYou may be running an incompatible version of Among Us.");
        }
        else
        {
            ResetLaunchWarning();
        }

        if (OperatingSystem.IsLinux())
        {
            var linuxDialog = new LinuxWarningDialog
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            linuxDialog.ShowDialog(this);
        }

        ProgressBar.ProgressTextFormat = "";

        var bepInExPlugins = new DirectoryInfo(Path.Combine(Constants.ModFolder, "BepInEx", "plugins"));
        
        if (!bepInExPlugins.Exists)
        {
            LauncherState = new InstallState(this);
            return;
        }
        
        var filesPresent = true;
        var updateRequired = false;
            
        foreach (var info in Config.ModPackData.ModList)
        {
            var pluginPath = Path.Combine(bepInExPlugins.FullName, info.Name);

            var updated = Utilities.IsPluginUpdated(pluginPath, info.Hash, out var exists);

            if (!exists)
            {
                Console.Out.WriteLine($"Missing {info.Name}");
                filesPresent = false;
            }

            if (!updated)
            {
                Console.Out.WriteLine($"Out of date: {info.Name}");
                updateRequired = true;
            }
        }

        if (filesPresent)
        {
            LauncherState = updateRequired ? new UpdateState(this) : new LaunchState(this);
        }
        else
        {
            LauncherState = new InstallState(this);
        }
    }
    
    public async void InstallClickHandler(object sender, RoutedEventArgs args)
    {
        ProgressBar.Value = 0;
        await LauncherState.ButtonClick();
    }

    public void SetInfoToPath()
    {
        InfoIcon.IsVisible = false;
        InfoText.Foreground = Brush.Parse("#555");
        InfoText.Text = $"Platform: {Config.Platform}\n{Config.AmongUsPath}";
    }

    public void ResetLaunchWarning()
    {
        LaunchWarning.Text = "Launching with mods takes time!\nPlease be patient.";
        LaunchWarning.IsVisible = false;
    }

    public void SetLaunchWarning(string text)
    {
        LaunchWarning.Text = text;
        LaunchWarning.IsVisible = true;
    }

    public void AmongUsOnExit()
    {
        LaunchWarning.IsVisible = false;
        WindowState = WindowState.Normal;
        Topmost = true;
        Topmost = false;
        Activate();
        Show();
        
        LauncherState = new LoadingState(this);
        Uninstall();
    }
    
    private void Uninstall()
    {
        Console.Out.WriteLine("Uninstalling");

        // we manipulate the doorstop config for epic and steam, so we need to restore it
        if (AmongUsLocator.GetPlatform(Config.AmongUsPath, Config.ModPackData) is AmongUsPlatform.Epic or AmongUsPlatform.Steam)
        {
            var doorstopBackup = new FileInfo(Path.Combine(Config.AmongUsPath, "doorstop_config.ini.bak"));
            if (doorstopBackup.Exists)
            {
                Console.Out.WriteLine("Restoring doorstop config");
                var doorstopConfig = new FileInfo(Path.Combine(Config.AmongUsPath, "doorstop_config.ini"));
                if (doorstopConfig.Exists)
                {
                    doorstopConfig.Delete();
                }

                doorstopBackup.MoveTo(Path.Combine(Config.AmongUsPath, "doorstop_config.ini"));
            }
            else
            {
                Console.Out.WriteLine("No doorstop config backup found, uninstalling completely");
                foreach (var file in Constants.UninstallPaths)
                {
                    var info = new FileInfo(Path.Combine(Config.AmongUsPath, file));
                    if (info.Exists)
                    {
                        info.Delete();
                    }
                }
            }
        }

        Console.Out.WriteLine("Uninstall complete, reloading AU path");
        LoadAmongUsPath();
    }
    
    private async void OpenDirectoryPicker(object? _, RoutedEventArgs e)
    {
        if (LauncherState is LoadingState or RunningState)
        {
            return;
        }
        
        var pattern = OperatingSystem.IsLinux() ? "Among?Us.exe" : "Among Us.exe";
        var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Among Us.exe",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Among Us"){Patterns = [pattern]}]
        });
        
        if (picked.Count <= 0)
        {
            return;
        }

        var file = new FileInfo(picked[0].Path.LocalPath);
        
        if (file.Directory is not null && AmongUsLocator.VerifyAmongUsDirectory(file.Directory.FullName))
        {
            Config.AmongUsPath = file.Directory.FullName;
            LoadAmongUsPath();
        }

    }

    private async void DiscordLinkOnClick(object? _, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://dsc.gg/allofus") {UseShellExecute = true});
        var clipboard = GetTopLevel(this)?.Clipboard;
        var dataObject = new DataObject();
        dataObject.Set(DataFormats.Text, "https://dsc.gg/allofus");
        if (clipboard is not null)
        {
            await clipboard.SetDataObjectAsync(dataObject);
        }
    }

    private void PointerDown(object? _, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }
}