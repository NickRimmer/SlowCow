using Microsoft.Extensions.Logging;
using SlowCow.Setup.Base.Interfaces;
using SlowCow.Setup.Repo.Base.Interfaces;
using SlowCow.Setup.Repo.Base.Models;
namespace SlowCow.Setup.Services;

internal class UpdatesInfoService
{
    private readonly IRepo _repo;
    private readonly IInstaller _installer;
    private readonly ILogger _logger;

    public UpdatesInfoService(IRepo repo, IInstaller installer, ILogger logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpdatesModel> GetInfoAsync()
    {
        var installedInfo = await _installer.GetReleaseInfoAsync(_logger);
        var lastRelease = await _repo.GetLastReleaseAsync(installedInfo?.Channel ?? RepoReleaseModel.DefaultChannel, _logger);
        var availableVersion = lastRelease?.Version;

        _logger.Log(LogLevel.Debug, "Installed version: {InstalledVersion}, Available version: {AvailableVersion}", installedInfo?.Version, availableVersion);

        return new UpdatesModel(
            installedInfo?.Version,
            availableVersion,
            !string.IsNullOrWhiteSpace(availableVersion) && availableVersion?.Equals(installedInfo?.Version, StringComparison.OrdinalIgnoreCase) != true,
            installedInfo?.Channel ?? RepoReleaseModel.DefaultChannel);
    }

    // ReSharper disable NotAccessedPositionalProperty.Global
    public record UpdatesModel(string? InstalledVersion, string? AvailableVersion, bool UpdateAvailable, string Channel);
}
