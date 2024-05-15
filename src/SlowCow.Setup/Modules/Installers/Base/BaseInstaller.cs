using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SlowCow.Setup.Modules.Installers.Models;
using SlowCow.Setup.Modules.Setups.Base.Models;
namespace SlowCow.Setup.Modules.Installers.Base;

internal abstract class BaseInstaller : IInstaller
{
    protected const string CurrentAppFolderName = "current";
    protected const string ManifestFileName = "manifest.json";

    public Task<InstallationModel?> GetInstallationAsync()
    {
        var installationPath = GetInstallationPath();
        var manifestFilePath = Path.Combine(installationPath, CurrentAppFolderName, ManifestFileName);
        if (!File.Exists(manifestFilePath)) return Task.FromResult<InstallationModel?>(null);

        var manifestJson = File.ReadAllText(manifestFilePath);
        var manifest = JsonConvert.DeserializeObject<ManifestModel>(manifestJson);

        return Task.FromResult(manifest == null ? null : new InstallationModel(manifest.Version, manifest.Channel));
    }

    public abstract Task InstallAsync(ManifestModel manifest, bool addDesktop, bool addStartMenu);

    public abstract Task UninstallAsync();

    public abstract string GetInstallationPath();
}
