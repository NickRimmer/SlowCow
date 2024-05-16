using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MsBox.Avalonia;
using SlowCow.Updater;
using SlowCow.Updater.Common;

namespace Example.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void CheckUpdates_OnClick(object? sender, RoutedEventArgs e)
    {
        var button = (Button) sender!;
        button.IsEnabled = false;
        Task.Run(async () =>
        {
            try
            {
                var updates = await SlowCowUpdater.GetVersionAsync();
                await Dispatcher.UIThread.InvokeAsync(() => MessageBoxManager
                    .GetMessageBoxStandard(
                        "Check for updates",
                        updates!.UpdateAvailable
                            ? $"An update is available. {updates.InstalledVersion} -> {updates.AvailableVersion}"
                            : $"No updates available. (Current version {updates.InstalledVersion})",
                        icon: MsBox.Avalonia.Enums.Icon.Info)
                    .ShowWindowDialogAsync(this));
            }
            catch (SlowCowException ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() => MessageBoxManager
                    .GetMessageBoxStandard("Cannot check for updates", ex.Message, icon: MsBox.Avalonia.Enums.Icon.Warning)
                    .ShowWindowDialogAsync(this));
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => button.IsEnabled = true);
            }
        }).ConfigureAwait(false);
    }
}
