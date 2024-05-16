using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using SlowCow.Setup.Base.Interfaces;
using SlowCow.Setup.Base.Models;
using SlowCow.Setup.Repo.Base.Interfaces;
using SlowCow.Setup.UI.ViewModels;
namespace SlowCow.Setup.UI.Views;

public partial class ActionInstallView : UserControl
{
    public ActionInstallView()
    {
        InitializeComponent();
    }

    private ActionInstallViewModel ViewModel => (ActionInstallViewModel) DataContext!;

    private void NextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.WizardStep++;
        if (ViewModel.WizardStep == 2)
        {
            var parentWindow = (MainWindow) this.FindAncestorOfType<Window>()!;
            StartInstallationAsync(ViewModel, parentWindow)
                .ContinueWith(_ => Dispatcher.UIThread.Invoke(() => ViewModel.WizardStep++), TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    private void ButtonOpenTargetFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        var directory = Path.GetDirectoryName(ViewModel.InstallationPath);
        if (directory != null && Directory.Exists(directory))
        {
            // open folder in file explorer if windows
            if (OperatingSystem.IsWindows())
            {
                Process.Start("explorer.exe", directory);
            }

            // open folder in file manager if linux
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", directory);
            }

            // open folder in file manager if mac
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", directory);
            }
        }
    }

    private static async Task StartInstallationAsync(ActionInstallViewModel viewModel, MainWindow mainWindow)
    {
        var logger = Runner.Services.GetRequiredService<ILogger>();
        try
        {
            var installer = Runner.Services.GetRequiredService<IInstaller>();
            var repo = Runner.Services.GetRequiredService<IRepo>();
            var runnerSettings = Runner.Services.GetRequiredService<RunnerSettingsModel>();

            var installedHashes = runnerSettings.HasRepairFlag
                ? new () // to load full pack
                : (await installer.GetReleaseInfoAsync(logger))?.Hashes ?? new (); // first installation

            using var downloadResult = await repo.DownloadAsync(viewModel.Release.Channel, viewModel.Release.Version, installedHashes, logger);
            if (downloadResult == null)
            {
                Dispatcher
                    .UIThread
                    .Invoke(() => mainWindow.ShowErrorPanel("Installation failed", "Cannot download files", null));
                return;
            }

            await installer.InstallAsync(downloadResult.PackStream, new InstallationSettingsModel {
                AddDesktop = viewModel.AddDesktopShortcut,
                AddStartMenu = viewModel.AddStartMenuShortcut,
                Channel = viewModel.Release.Channel,
                Version = viewModel.Release.Version,
                Hashes = downloadResult.Hashes,
            }, logger);
            logger.LogInformation("Installation completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Installation failed");
            Dispatcher
                .UIThread
                .Invoke(() => mainWindow.ShowErrorPanel(
                    "Installation failed",
                    $"{ex.Message}. ({ex.GetType().Name})",
                    ex.StackTrace));
        }
    }

    private void RunApp_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = Runner.Services.GetRequiredService<RunnerSettingsModel>();
        var appPath = Path.Combine(ViewModel.InstallationPath, "current", settings.ExecutableFileName);

        if (File.Exists(appPath))
        {
            Process.Start(appPath);
            Environment.Exit(0);
        }
        else
        {
            Runner.Services.GetRequiredService<ILogger>().LogError("Application executable not found: {AppPath}", appPath);
            MessageBoxManager.GetMessageBoxStandard("Cannot run application", $"Application executable not found.\n{appPath}", icon: Icon.Error)
                .ShowWindowDialogAsync(this.FindAncestorOfType<Window>());
        }
    }

    private void OpenReleaseNotes_OnClick(object? sender, RoutedEventArgs e)
    {
        // open url in system browserhow to open url in system browser, some cross-platform solution?
        if (Uri.TryCreate(ViewModel.Release.ReleaseNotes.Link, UriKind.Absolute, out var url))
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // For Windows
                    Process.Start(new ProcessStartInfo {
                        FileName = url.ToString(),
                        UseShellExecute = true,
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // For macOS
                    Process.Start("open", url.ToString());
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // For Linux
                    Process.Start("xdg-open", url.ToString());
                }
                else
                {
                    throw new PlatformNotSupportedException("Operating system not supported.");
                }
            }
            catch (Exception ex)
            {
                Runner.Services.GetRequiredService<ILogger>().LogError(ex, "An error occurred");
            }
        }
    }
}
