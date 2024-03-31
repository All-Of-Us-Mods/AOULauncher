using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Ionic.Zip;

namespace AOULauncher.Views;

public partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }
    
    public LauncherConfig Config;
    private ButtonState _buttonState;
    private List<FileHash>? _hashes;

    public ButtonState ButtonState
    {
        get => _buttonState;
        private set
        {
            _buttonState = value;
            UpdateButtonByState();
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        // Load config
        Config = File.Exists(Constants.ConfigPath)
            ? JsonSerializer.Deserialize(File.ReadAllText(Constants.ConfigPath), LauncherConfigContext.Default.LauncherConfig)
            : new LauncherConfig();

        Instance = this;
        
        // start downloading launcher data and load among us path to check for mod installation
        Task.Run(DownloadData);
    }

    public void UpdateProgress(int value)
    {
        ProgressBar.Value = value;
    }
    
    private async Task DownloadData()
    {
        try
        {
            Config.ModPackData = await NetworkManager.HttpClient.DownloadJson(Constants.ApiLocation, LauncherConfigContext.Default.ModPackData);
            _hashes = await NetworkManager.HttpClient.DownloadJson(Constants.HashLocation, FileHashListContext.Default.ListFileHash);
            await Dispatcher.UIThread.InvokeAsync(LoadAmongUsPath);
        }
        catch (Exception e)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Console.Out.WriteLine(e.ToString());
                Config.ModPackData = JsonSerializer.Deserialize(File.ReadAllText(Constants.ConfigPath), LauncherConfigContext.Default.LauncherConfig).ModPackData;
                LoadAmongUsPath();
            });
        }
    }
    
    private void LoadAmongUsPath()
    {
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
                ButtonState = ButtonState.Refresh;
                return;
            }
        }
       
        ProgressBar.ProgressTextFormat = "";

        var bepInExPlugins = new DirectoryInfo(Path.Combine(Constants.CachedModDirectory,"BepInEx","plugins"));
        
        if (!bepInExPlugins.Exists)
        {
            ButtonState = ButtonState.Install;
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
                Console.Out.WriteLine(info.Name);
                filesPresent = false;
            }

            if (!updated)
            {
                Console.Out.WriteLine(info.Name);
                updateRequired = true;
            }
        }

        if (filesPresent)
        {
            ButtonState = updateRequired ? ButtonState.Update : ButtonState.Launch;
        }
        else
        {
            ButtonState = ButtonState.Install;
        }
    }
    
    public async void InstallClickHandler(object sender, RoutedEventArgs args)
    {
        await ClickHandler();
    }

    private async Task ClickHandler()
    {
        ProgressBar.Value = 0;
        switch (ButtonState)
        {
            case ButtonState.Install:
            case ButtonState.Update:
                ButtonState = ButtonState.Loading;
                await InstallMod();

                LoadAmongUsPath();
                break;
            
            case ButtonState.Launch:
                ButtonState = ButtonState.Loading;
                await Launch();
                break;
            
            case ButtonState.Refresh:
                ButtonState = ButtonState.Loading;
                LoadAmongUsPath();
                break;

            case ButtonState.Loading:
            case ButtonState.Running:
                break;

            default:
                throw new Exception("Invalid button state");
        }
    }
    

    private async Task InstallMod()
    {
        Utilities.KillAmongUs();
        
        await InstallZip("BepInEx.zip", Constants.CachedModDirectory, Config.ModPackData.BepInEx);
        await InstallPlugins(Constants.CachedModDirectory);
        await InstallZip("ExtraData.zip", Constants.CachedModDirectory, Config.ModPackData.ExtraData);
    }

    private async Task VerifyBepInEx()
    {
        if (_hashes is null)
        {
            await InstallZip("BepInEx.zip", Config.AmongUsPath, Config.ModPackData.BepInEx);
            return;
        }
        
        float processed = 0;
        var total = _hashes.Count;
        foreach (var hash in _hashes)
        {
            var gamePath = new FileInfo(Path.GetFullPath(hash.RelativePath, Config.AmongUsPath));
            var gameHash = Utilities.FileToHash(gamePath.FullName);
            if (!hash.Hash.Equals(gameHash, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(gamePath.Directory!.FullName);
                File.Copy(Path.GetFullPath(hash.RelativePath, Constants.CachedModDirectory), gamePath.FullName, true);
            }

            Dispatcher.UIThread.Invoke(() => UpdateProgress((int)(100 * (processed++ / total))));
        }
    }
    
    
    private async Task InstallZip(string name, string directory, ModPackData.ZipData zipData)
    {
        var zipFile = await NetworkManager.HttpClient.DownloadZip(name, Constants.DataLocation, zipData);
        
        ProgressBar.ProgressTextFormat = $"Installing {name}";
 
        zipFile.ExtractProgress += (_, args) =>
        {
            if (args.EntriesTotal != 0)
            {
                ProgressBar.Value = 100 * ((float)args.EntriesExtracted / args.EntriesTotal);
            }
        };
        zipFile.ExtractAll(directory, ExtractExistingFileAction.OverwriteSilently);
        
        ProgressBar.ProgressTextFormat = $"Installed {name}";
    }
    
    
    private async Task InstallPlugins(string directory)
    {
        var pluginPath = Path.Combine(directory, "BepInEx", "plugins");
        
        ProgressBar.ProgressTextFormat = "Installing mod...";
        ProgressBar.Value = 0;
        
        foreach (var plugin in Config.ModPackData.ModList)
        {
            var path = Path.Combine(pluginPath, plugin.Name);
            if (File.Exists(path) && Utilities.IsPluginUpdated(path, plugin))
            {
                continue;
            }
            ProgressBar.ProgressTextFormat = $"Downloading {plugin.Name}...";

            await NetworkManager.HttpClient.DownloadFile(plugin.Name, pluginPath, plugin.Download);
        }
        ProgressBar.ProgressTextFormat = "Installed plugins";
    }

    private void UpdateButtonByState()
    {
        switch (ButtonState)
        {
            case ButtonState.Refresh:
                InfoIcon.IsVisible = true;
                InfoText.Foreground = Brush.Parse("#FFBB00");
                InfoText.Text = "";
                InfoText.Inlines?.Add("Among Us could not be found. Run the game and \npress refresh or click ");
                InfoText.Inlines?.Add(new Run("here") {FontWeight = FontWeight.SemiBold});
                InfoText.Inlines?.Add(" to choose manually");
                break;
                
            case ButtonState.Running:
            case ButtonState.Loading:
                InstallButton.IsEnabled = false;
                SetInfoToPath();
                break;
            case ButtonState.Install:
            case ButtonState.Update:
            case ButtonState.Launch:
            default:
                InstallButton.IsEnabled = true;
                SetInfoToPath();
                break;
        }
            
        InstallButton.IsEnabled = ButtonState != ButtonState.Running;
        InstallText.Text = _buttonState.ToString();
    }

    private void SetInfoToPath()
    {
        InfoIcon.IsVisible = false;
        InfoText.Foreground = Brush.Parse("#555");
        InfoText.Text = Config.AmongUsPath;
    }
    
    private async Task Launch()
    {
        Utilities.KillAmongUs();
        
        await VerifyBepInEx();

        Utilities.BackupFolder(Path.Combine(Config.AmongUsPath,"BepInEx"), "plugins");
        Utilities.BackupFolder(Path.Combine(Config.AmongUsPath,"BepInEx"), "config");
       
        CopyCacheFolder(Path.Combine("BepInEx","plugins"));
        CopyCacheFolder(Path.Combine("BepInEx","config"));
        
        ButtonState = ButtonState.Running;
        ProgressBar.ProgressTextFormat = "Running...";

        if (AmongUsLocator.GetPlatform(Config.AmongUsPath) == AmongUsPlatform.Epic)
        {
            EpicLaunch();
            return;
        }

        NormalLaunch();
    }

    private void NormalLaunch()
    {
        var process = Process.Start(Path.Combine(Config.AmongUsPath,"Among Us.exe")); 
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => Dispatcher.UIThread.InvokeAsync(AmongUsOnExit);
    }

    private void EpicLaunch()
    {
        var psi = new ProcessStartInfo(
            "com.epicgames.launcher://apps/33956bcb55d4452d8c47e16b94e294bd%3A729a86a5146640a2ace9e8c595414c56%3A963137e4c29d4c79a81323b8fab03a40?action=launch&silent=true")
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
            process.Exited += (_, _) => Dispatcher.UIThread.InvokeAsync(AmongUsOnExit);
            return;
        }
        
        AmongUsOnExit();
    }
    
    private void CopyCacheFolder(string folderPath)
    {
        var cache = Path.GetFullPath(folderPath, Constants.CachedModDirectory);
        var destination = Path.GetFullPath(folderPath, Config.AmongUsPath);
        Directory.CreateDirectory(destination);

        Utilities.CopyDirectory(cache, destination);
    }
    
    private void AmongUsOnExit()
    {
        Utilities.RestoreBackupFolder(Path.Combine(Config.AmongUsPath,"BepInEx"), "plugins");
        Utilities.RestoreBackupFolder(Path.Combine(Config.AmongUsPath,"BepInEx"), "config");

        WindowState = WindowState.Normal;
        Topmost = true;
        Topmost = false;
        Activate();
        Show();
        
        ButtonState = ButtonState.Loading;
        LoadAmongUsPath();
    }

    private async void OpenDirectoryPicker(object? _, RoutedEventArgs e)
    {
        if (ButtonState is ButtonState.Loading or ButtonState.Running)
        {
            return;
        }
        
        var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Among Us.exe",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Among Us"){Patterns = ["Among Us.exe"]}]
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