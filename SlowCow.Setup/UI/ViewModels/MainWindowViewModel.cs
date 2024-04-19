using ReactiveUI;
namespace SlowCow.Setup.UI.ViewModels;

internal class MainWindowViewModel : ViewModelBase
{
    private string _appName = string.Empty;
    private string _description = string.Empty;
    private bool _started;

    private ActionErrorViewModel? _errorData;
    private ActionInstallViewModel? _installationData;
    private ActionAlreadyInstalledViewModel? _alreadyInstalledData;

    public required string AppName
    {
        get => _appName;
        set => this.RaiseAndSetIfChanged(ref _appName, value);
    }

    public required string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    public bool Started
    {
        get => _started;
        set => this.RaiseAndSetIfChanged(ref _started, value);
    }

    public ActionInstallViewModel? InstallationData
    {
        get => _installationData;
        set => this.RaiseAndSetIfChanged(ref _installationData, value);
    }

    public ActionErrorViewModel? ErrorData
    {
        get => _errorData;
        set => this.RaiseAndSetIfChanged(ref _errorData, value);
    }

    public ActionAlreadyInstalledViewModel? AlreadyInstalledData
    {
        get => _alreadyInstalledData;
        set => this.RaiseAndSetIfChanged(ref _alreadyInstalledData, value);
    }

    public static MainWindowViewModel DesignInstance { get; } = new () {
        AppName = "Design Mode",
        Description = "This is description for design mode. It will be displayed here.",
        // InstallationData = ActionInstallViewModel.DesignInstance,
        // ErrorData = ActionErrorViewModel.DesignInstance,
    };
}
