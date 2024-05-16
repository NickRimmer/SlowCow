using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using SlowCow.Setup.Modules.Installers;
using SlowCow.Setup.Modules.Runner;
using SlowCow.Setup.Modules.Setups.Base;
using SlowCow.Setup.Modules.Setups.Base.Models;
using SlowCow.Setup.Modules.Updates;
using SlowCow.Setup.UI.ViewModels;
namespace SlowCow.Setup.UI.Views;

// can be internal, but requires to be public for visual designer
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            StartInDesignAsync().ConfigureAwait(false);
        }
        else
        {
            StartAsync().ConfigureAwait(false);
        }
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel) DataContext!;

    private async Task StartAsync()
    {
        try
        {
            var runnerSettings = Runner.Services.GetRequiredService<RunnerModel>();
            if (!string.IsNullOrWhiteSpace(runnerSettings.ParentProcessId))
            {
                // If the setup is started by another process, check if process with id started and wait for it to exit
                var processId = int.Parse(runnerSettings.ParentProcessId);
                var parentProcess = Process.GetProcesses().FirstOrDefault(x => x.Id == processId);
                if (parentProcess != null)
                {
                    LoadingView.ActionLoadingPanel.Content = "Please Wait...";
                    LoadingView.DetailsLabel.Content = "The application is shutting down to apply updates";
                    await parentProcess.WaitForExitAsync();
                }
            }

            var versionsManager = Runner.Services.GetRequiredService<UpdatesInfoService>();
            var setupProvider = Runner.Services.GetRequiredService<ISetup>();
            var installer = Runner.Services.GetRequiredService<InstallerProvider>().GetInstaller();

            var manifest = await setupProvider.LoadManifestAsync(runnerSettings.Channel) ?? throw new FileNotFoundException("Cannot read version details.");
            var installationPath = installer.GetInstallationPath();

            var versionInfo = await versionsManager.GetInfoAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ViewModel.Started = true;
                if (!versionInfo.UpdateAvailable && !runnerSettings.HasRepairFlag)
                {
                    ShowAlreadyInstalledPanel(versionInfo.InstalledVersion ?? "Not installed", installationPath);
                }
                else
                {
                    ShowInstallationPanel(manifest, installationPath);
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => ShowErrorPanel(
                "Cannot load manifest file",
                $"{ex.InnerException?.Message ?? ex.Message} ({ex.InnerException?.GetType().Name ?? ex.GetType().Name})",
                ex.StackTrace));
        }
    }

    private async Task StartInDesignAsync()
    {
        await Task.Delay(1000);
        await Dispatcher.UIThread.InvokeAsync(() => ShowInstallationPanel(new ManifestModel {
            Version = "1.2.3-design",
            Channel = RunnerModel.DefaultChannel,
            ReleaseNotes = new ManifestModel.ReleaseNotesModel {
                Text = "This is a design-time release notes message.",
            },
        }, string.Empty));
    }

    private void ShowInstallationPanel(ManifestModel manifest, string installationPath) => ViewModel.InstallationData = new ActionInstallViewModel {
        Manifest = manifest,
        InstallationPath = installationPath,
        AppName = ViewModel.AppName,
    };

    private void ShowAlreadyInstalledPanel(string installedVersion, string installationPath) => ViewModel.AlreadyInstalledData = new ActionAlreadyInstalledViewModel {
        InstalledVersion = installedVersion,
        InstallationPath = installationPath,
    };

    internal void ShowErrorPanel(string message, string details, string? stackTrace) => ViewModel.ErrorData = new ActionErrorViewModel {
        Message = message,
        Details = details,
        StackTrace = stackTrace ?? string.Empty,
    };
}
