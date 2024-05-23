using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SlowCow.Setup.Repo.Base;
using SlowCow.Setup.Repo.Base.Exceptions;
using SlowCow.Setup.Repo.Base.Interfaces;
using SlowCow.Setup.Repo.Base.Models;
using SlowCow.Setup.Repo.LocalFiles.Models;
namespace SlowCow.Setup.Repo.LocalFiles;

public class LocalRepo : IRepo
{
    private const string FullPackFileName = "pack-full";
    private const string DiffPackFileName = "pack-diff";

    private const string ChannelsDirectoryName = "channels";
    private const string VersionSettingsFileName = "release.json";
    private const string RepoSettingsFileName = "repo-settings.json";

    private static readonly JsonSerializerSettings JsonSettings = new () {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    private readonly string _repoPath;
    private RepoReleaseModel? _cachedManifest;

    public LocalRepo(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath)) throw new ArgumentException("Repo path cannot be null or whitespace.", nameof(repoPath));
        _repoPath = repoPath;
    }

    public async Task<RepoReleaseModel?> GetLastReleaseAsync(string channel, ILogger logger)
    {
        if (_cachedManifest != null) return _cachedManifest;

        var repoSettings = ReadRepoSettings();
        if (!repoSettings.Channels.TryGetValue(channel, out var versions)) return null;

        var lastVersion = versions.LastOrDefault();
        if (lastVersion == null) return null;

        var releaseInfoPath = Path.Combine(_repoPath, ChannelsDirectoryName, channel, lastVersion, VersionSettingsFileName);
        if (!File.Exists(releaseInfoPath)) return null;

        var versionSettingsJson = await File.ReadAllTextAsync(releaseInfoPath);
        var versionSettings = JsonConvert.DeserializeObject<RepoReleaseModel>(versionSettingsJson);
        _cachedManifest = versionSettings;
        return _cachedManifest;
    }

    public async Task<bool> UploadAsync(RepoReleaseModel releaseInfo, string sourcePath, ILogger logger)
    {
        var version = BuildVersionString(releaseInfo);
        releaseInfo = releaseInfo with {
            Version = version,
        };
        var versionDirectory = Path.Combine(_repoPath, ChannelsDirectoryName, releaseInfo.Channel, version);

        if (Directory.Exists(versionDirectory))
        {
            // delete existing version
            Directory.Delete(versionDirectory, true);
        }

        // pack files
        var previousVersionHashes = GetPreviousVersionHashes(releaseInfo);
        using var packData = await new Packer(logger).PackAsync(sourcePath, previousVersionHashes);
        if (packData == null || packData.FullPack.Hashes.Count == 0) return false;

        // save pack to target directory
        Directory.CreateDirectory(versionDirectory);
        await File.WriteAllTextAsync(Path.Combine(versionDirectory, VersionSettingsFileName), JsonConvert.SerializeObject(releaseInfo, JsonSettings));
        File.Copy(packData.FullPack.TempFilePath, Path.Combine(versionDirectory, $"{FullPackFileName}.zip"));
        await File.WriteAllTextAsync(Path.Combine(versionDirectory, $"{FullPackFileName}.json"), JsonConvert.SerializeObject(packData.FullPack.Hashes, Formatting.Indented));
        if (packData.DiffPack?.Hashes.Count > 0)
        {
            File.Copy(packData.DiffPack.TempFilePath, Path.Combine(versionDirectory, $"{DiffPackFileName}.zip"));
            await File.WriteAllTextAsync(Path.Combine(versionDirectory, $"{DiffPackFileName}.json"), JsonConvert.SerializeObject(packData.DiffPack.Hashes, Formatting.Indented));
        }

        // update repo versions
        var repoSettings = ReadRepoSettings();
        if (!repoSettings.Channels.ContainsKey(releaseInfo.Channel))
            repoSettings.Channels[releaseInfo.Channel] = [];

        repoSettings.Channels[releaseInfo.Channel].Add(version);
        await WriteRepoSettings(repoSettings);

        return true;
    }

    public Task<DownloadResultModel?> DownloadAsync(string channel, string loadVersion, Dictionary<string, string> installedHashes, ILogger logger)
    {
        DownloadResultModel? result = null;

        // when loadVersion is null or whitespace, cancel loading
        if (string.IsNullOrWhiteSpace(loadVersion)) return Task.FromResult(result);

        // if read available versions from repo
        var repoSettings = ReadRepoSettings();
        if (!repoSettings.Channels.TryGetValue(channel, out var versions) || !versions.Any(x => x.Equals(loadVersion, StringComparison.Ordinal)))
            return Task.FromResult(result);

        // if directory does not exist
        var versionDirectory = Path.Combine(_repoPath, ChannelsDirectoryName, channel, loadVersion);
        if (!Directory.Exists(versionDirectory)) return Task.FromResult(result);

        var downloadDiffPack = IsUpgradable(versionDirectory, installedHashes, out var fullPackHashes);
        var packPath = Path.Combine(versionDirectory, downloadDiffPack ? $"{DiffPackFileName}.zip" : $"{FullPackFileName}.zip");

        // when diff file not found
        if (downloadDiffPack && !File.Exists(packPath)) packPath = Path.Combine(versionDirectory, $"{FullPackFileName}.zip");

        // when version file not found
        if (!File.Exists(packPath)) return Task.FromResult(result);

        result = new DownloadResultModel {
            PackStream = new FileStream(packPath, FileMode.Open, FileAccess.Read),
            Hashes = fullPackHashes ?? new (),
        };
        return Task.FromResult(result)!;
    }

    private RepoSettingsModel ReadRepoSettings()
    {
        var repoSettingsPath = GetRepoSettingsFilePath();
        return File.Exists(repoSettingsPath)
            ? JsonConvert.DeserializeObject<RepoSettingsModel>(File.ReadAllText(repoSettingsPath)) ?? throw new RepoException("Failed to read repo settings")
            : new RepoSettingsModel();
    }

    private Task WriteRepoSettings(RepoSettingsModel repoSettingsSettings)
    {
        var repoSettingsPath = GetRepoSettingsFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(repoSettingsPath)!);
        return File.WriteAllTextAsync(repoSettingsPath, JsonConvert.SerializeObject(repoSettingsSettings, JsonSettings));
    }

    private string GetRepoSettingsFilePath() => Path.Combine(_repoPath, RepoSettingsFileName);

    private Dictionary<string, string> GetPreviousVersionHashes(RepoReleaseModel settings)
    {
        var repoSettings = ReadRepoSettings();
        var lastVersion = repoSettings.Channels.TryGetValue(settings.Channel, out var versions)
            ? versions.LastOrDefault()
            : null;

        if (lastVersion == null) return new ();

        var hashesFilePath = Path.Combine(_repoPath, ChannelsDirectoryName, settings.Channel, lastVersion, $"{FullPackFileName}.json");
        var hashesJson = File.ReadAllText(hashesFilePath);
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(hashesJson) ?? new ();
    }

    private string BuildVersionString(RepoReleaseModel settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Version)) return settings.Version;
        var repoSettings = ReadRepoSettings();

        // if channel does not exist, return 1.0.0
        if (!repoSettings.Channels.TryGetValue(settings.Channel, out var versions)) return "1.0.0";

        // find last version
        var version = versions.LastOrDefault();
        if (string.IsNullOrWhiteSpace(version)) return "1.0.0";

        // try parse version
        var versionParts = version.Split('.');
        if (versionParts.Length != 3) return "1.0.0";

        // increment patch version
        if (int.TryParse(versionParts[2], out var patch))
        {
            patch++;
            return $"{versionParts[0]}.{versionParts[1]}.{patch}";
        }

        return "1.0.0";
    }

    private static bool IsUpgradable(string versionDirectory, Dictionary<string, string> installedHashes, [NotNullWhen(true)] out Dictionary<string, string>? fullPackHashes)
    {
        fullPackHashes = null;

        var fullPackPath = Path.Combine(versionDirectory, $"{FullPackFileName}.json");
        if (!File.Exists(fullPackPath)) return false;
        var fullPackJson = File.ReadAllText(fullPackPath);
        fullPackHashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(fullPackJson) ?? new ();
        if (fullPackHashes.Count == 0) return false;

        var diffPackPath = Path.Combine(versionDirectory, $"{DiffPackFileName}.json");
        if (!File.Exists(diffPackPath)) return false;
        var diffPackJson = File.ReadAllText(diffPackPath);
        var diffPackHashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(diffPackJson) ?? new ();
        if (diffPackHashes.Count == 0) return false;

        var actualDiffHashes = fullPackHashes.Where(x => !installedHashes.TryGetValue(x.Key, out var hash) && hash?.Equals(x.Value, StringComparison.Ordinal) != true).ToDictionary(x => x.Key, x => x.Value);
        return actualDiffHashes.All(actualDiff => diffPackHashes.ContainsKey(actualDiff.Key));
    }
}
