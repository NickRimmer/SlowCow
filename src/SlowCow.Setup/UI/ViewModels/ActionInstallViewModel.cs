using System;
using System.IO;
using ReactiveUI;
using SlowCow.Setup.Modules.Runner;
using SlowCow.Setup.Modules.Setups.Base.Models;
namespace SlowCow.Setup.UI.ViewModels;

internal class ActionInstallViewModel : ViewModelBase
{
    private int _wizardStep;
    private bool _addDesktopShortcut = true;
    private bool _addStartMenuShortcut;
    private string _installationPath = string.Empty;
    private string _applicationName = string.Empty;

    public required ManifestModel Manifest { get; init; }

    public int WizardStep
    {
        get => _wizardStep;
        set => this.RaiseAndSetIfChanged(ref _wizardStep, value);
    }

    public bool AddDesktopShortcut
    {
        get => _addDesktopShortcut;
        set => this.RaiseAndSetIfChanged(ref _addDesktopShortcut, value);
    }

    public bool AddStartMenuShortcut
    {
        get => _addStartMenuShortcut;
        set => this.RaiseAndSetIfChanged(ref _addStartMenuShortcut, value);
    }

    public string InstallationPath
    {
        get => _installationPath;
        set => this.RaiseAndSetIfChanged(ref _installationPath, value);
    }

    public string AppName
    {
        get => _applicationName;
        set => this.RaiseAndSetIfChanged(ref _applicationName, value);
    }

    public static ActionInstallViewModel? DesignInstance { get; } = new () {
        Manifest = new ManifestModel {
            Version = "1.2.3-design",
            Channel = RunnerModel.DefaultChannel,
            ReleaseNotes = new ManifestModel.ReleaseNotesModel {
                Text = "Please provide release notes for this version. It will be displayed here.",
            },
        },
        AppName = "Design app",
        WizardStep = 3,
        InstallationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Design app"),
    };
}
