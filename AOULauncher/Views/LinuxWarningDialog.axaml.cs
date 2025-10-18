using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Input;

namespace AOULauncher.Views;

public partial class LinuxWarningDialog : Window
{
    public LinuxWarningDialog()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private async void CopyButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var textBox = this.FindControl<TextBox>("CommandText");
            if (textBox == null) return;

            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard == null) return;
            await clipboard.SetTextAsync(textBox.Text);

            var copyText = this.FindControl<TextBlock>("CopyText");
            var copiedText = this.FindControl<TextBlock>("CopiedText");

            if (copyText == null || copiedText == null) return;
            copyText.IsVisible = false;
            copiedText.IsVisible = true;

            await Task.Delay(1500);

            copyText.IsVisible = true;
            copiedText.IsVisible = false;
        }
        catch (Exception ex)
        {
            await Console.Out.WriteLineAsync($"{ex}");
        }
    }

    private async void ProtontricksLink_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://docs.bepinex.dev/articles/advanced/proton_wine.html",
            UseShellExecute = true
        });
        var clipboard = GetTopLevel(this)?.Clipboard;
        var dataObject = new DataObject();
        dataObject.Set(DataFormats.Text, "https://docs.bepinex.dev/articles/advanced/proton_wine.html");
        if (clipboard is not null)
        {
            await clipboard.SetDataObjectAsync(dataObject);
        }
    }
}