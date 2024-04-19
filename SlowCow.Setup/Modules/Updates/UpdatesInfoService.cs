using System;
using System.Threading.Tasks;
using SlowCow.Setup.Modules.Installers;
using SlowCow.Setup.Modules.Runner;
using SlowCow.Setup.Modules.Setups.Base;
namespace SlowCow.Setup.Modules.Updates;

internal class UpdatesInfoService
{
    private readonly ISetup _setup;
    private readonly InstallerProvider _installerProvider;
    public UpdatesInfoService(ISetup setup, InstallerProvider installerProvider)
    {
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _installerProvider = installerProvider ?? throw new ArgumentNullException(nameof(installerProvider));
    }

    public async Task<UpdatesModel> GetInfoAsync()
    {
        var installationInfo = await _installerProvider.GetInstaller().GetInstallationAsync();
        var manifest = await _setup.LoadManifestAsync(installationInfo?.Channel ?? RunnerModel.DefaultChannel);
        var availableVersion = manifest?.Version;

        return new UpdatesModel(
            installationInfo?.Version,
            availableVersion,
            !string.IsNullOrWhiteSpace(availableVersion) && availableVersion?.Equals(installationInfo?.Version, StringComparison.OrdinalIgnoreCase) != true,
            installationInfo?.Channel ?? RunnerModel.DefaultChannel);
    }
}
