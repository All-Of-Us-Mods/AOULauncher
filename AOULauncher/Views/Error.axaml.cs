using Avalonia.Controls;

namespace AOULauncher.Views;

public partial class Error : Window
{
    public Error()
    {
        InitializeComponent();
        ErrorText.Text = "An error occurred.";
    }

    public Error(string error) : this()
    {
        ErrorText.Text = error;
    }
}