using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SlowCow.Setup.Modules.Setups.Base;
using SlowCow.Setup.Modules.Setups.Base.Exceptions;
using SlowCow.Setup.Modules.Setups.Base.Models;
using SlowCow.Setup.Modules.Setups.LocalSetup.Models;
namespace SlowCow.Setup.Modules.Setups.LocalSetup;

public class LocalSetup : ISetup
{
    private const string ChannelsDirectoryName = "channels";
    private const string VersionSettingsFileName = "version-settings.json";
    private const string RepoSettingsFileName = "repo-settings.json";

    private static readonly JsonSerializerSettings JsonSettings = new () {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    private readonly string _repoPath;
    private ManifestModel? _cachedManifest;

    public LocalSetup(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath)) throw new ArgumentException("Repo path cannot be null or whitespace.", nameof(repoPath));
        _repoPath = repoPath;
    }

    public async Task PackAsync(string settingsJson)
    {
        var settings = JsonConvert.DeserializeObject<LocalSetupPackModel>(settingsJson) ?? throw new PackerException("Failed to read settings from JSON");
        var version = GetVersion(settings);
        var versionDirectory = Path.Combine(_repoPath, ChannelsDirectoryName, settings.Channel, version);

        if (Directory.Exists(versionDirectory))
        {
            // delete existing version
            Directory.Delete(versionDirectory, true);
        }

        // pack files
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"slow-cow-setup-{Guid.NewGuid():N}");
        var previousVersionHashes = GetPreviousVersionHashes(settings);
        await DefaultPacker.PackAsync(settings.SourceDirectory, tempDirectory, previousVersionHashes);

        // move to target directory
        Directory.CreateDirectory(versionDirectory);
        foreach (var file in Directory.GetFiles(tempDirectory))
        {
            var fileName = Path.GetFileName(file);
            var targetPath = Path.Combine(versionDirectory, fileName);
            File.Move(file, targetPath);
        }

        var versionSettings = new LocalSetupVersionModel {
            ReleaseNotes = settings.ReleaseNotes,
        };
        await File.WriteAllTextAsync(Path.Combine(versionDirectory, VersionSettingsFileName), JsonConvert.SerializeObject(versionSettings, JsonSettings));

        // cleanup
        Directory.Delete(tempDirectory, true);

        // update repo
        var repoSettings = ReadRepoSettings();
        if (!repoSettings.Channels.ContainsKey(settings.Channel))
            repoSettings.Channels[settings.Channel] = new List<string>();

        repoSettings.Channels[settings.Channel].Add(version);
        await WriteRepoSettings(repoSettings);
    }

    public async Task<ManifestModel?> LoadManifestAsync(string channel)
    {
        if (_cachedManifest != null) return _cachedManifest;

        var repoSettings = ReadRepoSettings();
        if (!repoSettings.Channels.TryGetValue(channel, out var versions)) return null;

        var lastVersion = versions.LastOrDefault();
        if (lastVersion == null) return null;

        var versionSettingsPath = Path.Combine(_repoPath, ChannelsDirectoryName, channel, lastVersion, VersionSettingsFileName);
        if (!File.Exists(versionSettingsPath)) return null;

        var versionSettingsJson = await File.ReadAllTextAsync(versionSettingsPath);
        var versionSettings = JsonConvert.DeserializeObject<LocalSetupVersionModel>(versionSettingsJson);

        var releaseNotes = versionSettings?.ReleaseNotes.Count > 1
            ? string.Join("\n", versionSettings.ReleaseNotes.Select(x => $"- {x}"))
            : versionSettings?.ReleaseNotes.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(releaseNotes)) releaseNotes = "No release notes provided.";
        _cachedManifest = new ManifestModel {
            Version = lastVersion,
            Channel = channel,
            ReleaseNotes = new ManifestModel.ReleaseNotesModel {
                Text = releaseNotes,
            },
        };

        return _cachedManifest;
    }

    public Task<byte[]> LoadPackFileAsync(string channel, string loadVersion, string? currentVersion = null)
    {
        // when loadVersion is null or whitespace, cancel loading
        if (string.IsNullOrWhiteSpace(loadVersion)) return Task.FromResult(Array.Empty<byte>());

        // if currentVersion is same to loadVersion, cancel loading
        if (currentVersion != null && loadVersion.Equals(currentVersion, StringComparison.Ordinal)) return Task.FromResult(Array.Empty<byte>());

        var repoSettings = ReadRepoSettings();

        // if version not found
        if (!repoSettings.Channels.TryGetValue(channel, out var versions) || !versions.Any(x => x.Equals(loadVersion, StringComparison.Ordinal)))
            return Task.FromResult(Array.Empty<byte>());

        // if directory does not exist
        var versionDirectory = Path.Combine(_repoPath, ChannelsDirectoryName, channel, loadVersion);
        if (!Directory.Exists(versionDirectory)) return Task.FromResult(Array.Empty<byte>());

        var fullPack = currentVersion == null || versions.Count < 2 || !versions[^1].Equals(currentVersion, StringComparison.Ordinal);
        var packPath = Path.Combine(versionDirectory, fullPack ? $"{DefaultPacker.FullPackName}.zip" : $"{DefaultPacker.DiffPackName}.zip");

        // when diff file not found
        if (!fullPack && !File.Exists(packPath)) packPath = Path.Combine(versionDirectory, $"{DefaultPacker.FullPackName}.zip");

        // when version file not found
        if (!File.Exists(packPath)) return Task.FromResult(Array.Empty<byte>());
        return File.ReadAllBytesAsync(packPath);
    }

    private LocalSetupRepoModel ReadRepoSettings()
    {
        var repoSettingsPath = GetRepoSettingsFilePath();
        return File.Exists(repoSettingsPath)
            ? JsonConvert.DeserializeObject<LocalSetupRepoModel>(File.ReadAllText(repoSettingsPath)) ?? throw new LoaderException("Failed to read repo settings")
            : new LocalSetupRepoModel();
    }

    private Task WriteRepoSettings(LocalSetupRepoModel repoSettings)
    {
        var repoSettingsPath = GetRepoSettingsFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(repoSettingsPath)!);
        return File.WriteAllTextAsync(repoSettingsPath, JsonConvert.SerializeObject(repoSettings, JsonSettings));
    }

    private string GetRepoSettingsFilePath() => Path.Combine(_repoPath, RepoSettingsFileName);

    private Dictionary<string, string> GetPreviousVersionHashes(LocalSetupPackModel settings)
    {
        var repoSettings = ReadRepoSettings();
        var lastVersion = repoSettings.Channels.TryGetValue(settings.Channel, out var versions)
            ? versions.LastOrDefault()
            : null;

        if (lastVersion == null) return new ();

        var hashesFilePath = Path.Combine(_repoPath, ChannelsDirectoryName, settings.Channel, lastVersion, $"{DefaultPacker.FullPackName}.json");
        var hashesJson = File.ReadAllText(hashesFilePath);
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(hashesJson) ?? new ();
    }

    private string GetVersion(LocalSetupPackModel settings)
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
}
