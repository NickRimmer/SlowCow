using Microsoft.Extensions.Logging;
using SlowCow.Setup.Base.Models;
namespace SlowCow.Setup.Base.Interfaces;

public interface IInstaller
{
    Task InstallAsync(Stream packStream, InstallationSettingsModel settings, ILogger logger);
    Task<bool> UninstallAsync(ILogger logger);
    Task<ReleaseInfoModel?> GetReleaseInfoAsync(ILogger logger);
    string InstallationPath { get; }
}
