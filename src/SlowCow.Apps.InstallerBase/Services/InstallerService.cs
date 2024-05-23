using SlowCow.Apps.InstallerBase.Models;
using SlowCow.Installers.Base.Interfaces;
using SlowCow.Libs.Install;
using SlowCow.Repo.Base.Interfaces;
namespace SlowCow.Apps.InstallerBase.Services;

public static class InstallerService
{
    public static async Task RunAsync(IRepo repo, IInstaller installer, AppSettingsModel appSettings)
    {
        var installService = new SlowCowInstall(repo, installer);
        var result = await installService.InstallAsync(appSettings.AddDesktopShortcut, appSettings.AddStartMenuShortcut);

        if (result)
        {
            Log.Information("Installation/Update successful");
        }
        else
        {
            Log.Error("Installation/Update failed");
        }
    }
}
