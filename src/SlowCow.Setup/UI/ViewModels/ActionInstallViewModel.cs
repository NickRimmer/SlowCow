using ReactiveUI;
using SlowCow.Setup.Repo.Base.Models;
namespace SlowCow.Setup.UI.ViewModels;

internal class ActionInstallViewModel : ViewModelBase
{
    private int _wizardStep;
    private bool _addDesktopShortcut = true;
    private bool _addStartMenuShortcut;
    private string _installationPath = string.Empty;
    private string _applicationName = string.Empty;

    public required RepoReleaseModel Release { get; init; }

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
        Release = new RepoReleaseModel {
            Version = "1.2.3",
            Channel = RunnerSettingsModel.DefaultChannel,
            ReleaseNotes = new RepoReleaseModel.ReleaseNotesModel {
                Text = "Please provide release notes for this version. It will be displayed here.",
            },
        },
        AppName = "Design app",
        WizardStep = 0,
        InstallationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Design app"),
    };
}
