using Avalonia.Controls;
using Avalonia.Interactivity;
using SlowCow.Setup.UI.ViewModels;

namespace SlowCow.Setup.UI.Views;

public partial class ActionAlreadyInstalled : UserControl
{
    public ActionAlreadyInstalled()
    {
        InitializeComponent();
    }

    private ActionAlreadyInstalledViewModel ViewModel => (ActionAlreadyInstalledViewModel) DataContext!;

    private void ButtonOpenTargetFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        var directory = ViewModel.InstallationPath;
        if (directory != null && Directory.Exists(directory))
        {
            // open folder in file explorer if windows
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start("explorer.exe", directory);
            }

            // open folder in file manager if linux
            else if (OperatingSystem.IsLinux())
            {
                System.Diagnostics.Process.Start("xdg-open", directory);
            }

            // open folder in file manager if mac
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", directory);
            }
        }

        Environment.Exit(0);
    }
}
