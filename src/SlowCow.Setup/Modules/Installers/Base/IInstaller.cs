using System.Threading.Tasks;
using SlowCow.Setup.Modules.Installers.Models;
using SlowCow.Setup.Modules.Setups.Base.Models;
namespace SlowCow.Setup.Modules.Installers.Base;

internal interface IInstaller
{
    Task InstallAsync(ManifestModel manifest, bool addDesktop, bool addStartMenu);
    Task UninstallAsync();
    Task<InstallationModel?> GetInstallationAsync();
    string GetInstallationPath();
}
