using Avalonia.Controls;
using Avalonia.Interactivity;
using MsBox.Avalonia;

namespace Example.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void CheckUpdates_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var updates = Updater.GetUpdates();
            MessageBoxManager
                .GetMessageBoxStandard(
                    "Check for updates",
                    updates!.UpdateAvailable
                        ? $"An update is available. {updates.InstalledVersion} -> {updates.AvailableVersion}"
                        : $"No updates available. (Current version {updates.InstalledVersion})",
                    icon: MsBox.Avalonia.Enums.Icon.Info)
                .ShowWindowDialogAsync(this);
        }
        catch (Updater.UpdaterException ex)
        {
            MessageBoxManager
                .GetMessageBoxStandard("Cannot check for updates", ex.Message, icon: MsBox.Avalonia.Enums.Icon.Warning)
                .ShowWindowDialogAsync(this);
        }
    }
}
