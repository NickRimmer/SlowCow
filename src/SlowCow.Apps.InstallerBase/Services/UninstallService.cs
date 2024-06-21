using SlowCow.Apps.Shared.Services;
using SlowCow.Installers.Base;
using SlowCow.Installers.Base.Interfaces;
namespace SlowCow.Apps.InstallerBase.Services;

public static class UninstallService
{
    public static Task UninstallAsync(IInstaller installer) => installer.UninstallAsync();

    public static Task UninstallCleaningAsync(IInstaller installer, ArgsService argService)
    {
        if (!argService.TryGetValue(Constants.ParentProcessArgName, out var parentProcessId)) parentProcessId = string.Empty;
        return installer.UninstallCleaningAsync(argService.RawArguments, parentProcessId);
    }
}
