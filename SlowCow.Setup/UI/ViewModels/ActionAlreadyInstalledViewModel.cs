namespace SlowCow.Setup.UI.ViewModels;

internal record ActionAlreadyInstalledViewModel
{
    public required string InstalledVersion { get; init; }
    public required string InstallationPath { get; init; }

    public static ActionAlreadyInstalledViewModel DesignInstance { get; } = new () {
        InstalledVersion = "1.2.3-design",
        InstallationPath = "x:/y/z-design",
    };
}
