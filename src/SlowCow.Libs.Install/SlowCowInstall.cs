using Serilog;
using SlowCow.Installers.Base.Interfaces;
using SlowCow.Installers.Base.Models;
using SlowCow.Repo.Base.Interfaces;
using SlowCow.Repo.Base.Models;
namespace SlowCow.Libs.Install;

public class SlowCowInstall
{
    private readonly IRepo _repository;
    private readonly IInstaller _installer;

    public SlowCowInstall(IRepo repository, IInstaller installer)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
    }

    public async Task<bool> InstallAsync(bool addDesktopShortcut = true, bool addStartMenuShortcut = true)
    {
        // check if new version is available
        var latestVersion = await _repository.GetLatestVersionAsync(_installer.Settings.Channel);
        if (latestVersion == null)
        {
            Log.Error("Failed to get latest version in repo");
            return false;
        }
        var installedReleaseInfo = await _installer.GetInstalledReleaseInfoAsync();

        // download release
        using var releaseStream = await GetReleaseAsync(latestVersion.Version, installedReleaseInfo);
        if (releaseStream == PackedStreamInfo.Empty || releaseStream.PackStream == Stream.Null)
        {
            Log.Error("Failed to download release");
            return false;
        }

        // install/update release
        var fullHashes = releaseStream.IsFullPackage ? releaseStream.Hashes : await _repository.GetLatestFullHashesAsync(_installer.Settings.Channel);
        var releaseInfo = new InstalledReleaseInfo {
            Channel = latestVersion.Channel,
            Hashes = fullHashes ?? new (),
            Version = latestVersion.Version,
            ExecutableRelativePath = _installer.Settings.ExecutableRelativePath,
            ApplicationId = _installer.Settings.ApplicationId,
        };

        if (!await _installer.InstallAsync(releaseStream.PackStream, releaseStream.Hashes, releaseInfo, addDesktopShortcut, addStartMenuShortcut))
        {
            Log.Error("Failed to install release");
            return false;
        }

        return true;
    }

    private async Task<PackedStreamInfo> GetReleaseAsync(string version, InstalledReleaseInfo installedReleaseInfo)
    {
        var loadDiffOnly = await DiffHashesMatchAsync(installedReleaseInfo);
        var release = await _repository.DownloadPackageStreamAsync(version, _installer.Settings.Channel, !loadDiffOnly);

        if (release != null) return release;

        Log.Warning("Failed to download release");
        return PackedStreamInfo.Empty;
    }

    private async Task<bool> DiffHashesMatchAsync(InstalledReleaseInfo installedReleaseInfo)
    {
        // read hashes of installed files
        var currentHashes = installedReleaseInfo.Hashes;
        if (currentHashes is not { Count: > 0 })
        {
            Log.Information("No current hashes available. Full download required");
            return false;
        }

        // read diff hashes from repository
        var availableDiffHashes = await _repository.GetLatestDiffHashesAsync(_installer.Settings.Channel);
        if (availableDiffHashes is not { Count: > 0 })
        {
            Log.Information("No diff hashes available in repo. Full download required");
            return false;
        }

        // read full hashes from repository
        var availableFullHashes = await _repository.GetLatestFullHashesAsync(_installer.Settings.Channel);
        if (availableFullHashes is not { Count: > 0 })
        {
            Log.Warning("Cannot read available hashes from repository");
            return false;
        }

        // build real diff hashes of installed files
        var currentDiffHashes = availableFullHashes
            .Where(x => !currentHashes.ContainsKey(x.Key) || currentHashes[x.Key].Equals(x.Value, StringComparison.Ordinal))
            .ToDictionary(x => x.Key, x => x.Value);

        // make sure that repo diff contains all real diff hashes
        if (!currentDiffHashes.All(x => availableDiffHashes.ContainsKey(x.Key) && availableDiffHashes[x.Key].Equals(x.Value, StringComparison.Ordinal)))
        {
            Log.Information("Diff hashes mismatch. Full download required");
            return false;
        }

        // repo hashes has all real diff hashes and can be used for download
        return true;
    }
}
