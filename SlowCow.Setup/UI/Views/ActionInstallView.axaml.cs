using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using SlowCow.Setup.Modules.Installers;
using SlowCow.Setup.Modules.Runner;
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
        try
        {
            var installer = Runner.Services.GetRequiredService<InstallerProvider>().GetInstaller();
            await installer.InstallAsync(viewModel.Manifest, viewModel.AddDesktopShortcut, viewModel.AddStartMenuShortcut);
        }
        catch (Exception ex)
        {
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
        var settings = Runner.Services.GetRequiredService<RunnerModel>();
        var appPath = Path.Combine(ViewModel.InstallationPath, "current", settings.ExecutableFileName);

        if (File.Exists(appPath))
        {
            Process.Start(appPath);
            Environment.Exit(0);
        }
        else
        {
            MessageBoxManager.GetMessageBoxStandard("Cannot run application", $"Application executable not found.\n{appPath}", icon: Icon.Error)
                .ShowWindowDialogAsync(this.FindAncestorOfType<Window>());
        }
    }
}
