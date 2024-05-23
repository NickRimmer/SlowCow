using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MsBox.Avalonia;
using SlowCow.Setup.Base.Exceptions;
using SlowCow.Setup.Base.Interfaces;
using SlowCow.Setup.Base.Loggers;
using SlowCow.Setup.Windows.Updater;

namespace Example.App;

public partial class MainWindow : Window
{
    private readonly TempLogger _logger;
    private readonly IUpdater _updater;
    public MainWindow()
    {
        InitializeComponent();
        _logger = new TempLogger(Guid.Parse("7B0B8ADB-8F6F-4416-B7DB-9E773FD16DF6"));
        _updater = GetOsUpdater();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        Task.Run(() =>
        {
            var version = _updater.GetCurrentInfo()?.Version ?? "unknown version";
            Dispatcher.UIThread.Invoke(() => VersionLabel.Content = version);
        });

        base.OnLoaded(e);
    }

    private IUpdater GetOsUpdater()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsUpdater(_logger);
        throw new PlatformNotSupportedException("This platform is not supported");
    }

    private void CheckUpdates_OnClick(object? sender, RoutedEventArgs e)
    {
        var button = (Button) sender!;
        button.IsEnabled = false;
        Task.Run(async () =>
        {
            try
            {
                var updates = _updater.GetUpdateInfo();
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

    private void InstallUpdates_OnClick(object? sender, RoutedEventArgs e)
    {
        var button = (Button) sender!;
        button.IsEnabled = false;

        Task.Run(async () =>
        {
            try
            {
                if (!_updater.InstallLatest(force: false))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => MessageBoxManager
                        .GetMessageBoxStandard("Not updated", "No updates available", icon: MsBox.Avalonia.Enums.Icon.Info)
                        .ShowWindowDialogAsync(this));
                }
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
        });
    }

    private void ForceInstall_OnClick(object? sender, RoutedEventArgs e)
    {
        var button = (Button) sender!;
        button.IsEnabled = false;

        Task.Run(async () =>
        {
            try
            {
                if (!_updater.InstallLatest(force: true))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => MessageBoxManager
                        .GetMessageBoxStandard("Not updated", "No updates available", icon: MsBox.Avalonia.Enums.Icon.Info)
                        .ShowWindowDialogAsync(this));
                }
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
        });
    }
}
