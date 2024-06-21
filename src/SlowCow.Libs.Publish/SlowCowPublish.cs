using Serilog;
using SlowCow.Repo.Base.Interfaces;
using SlowCow.Repo.Base.Models;
namespace SlowCow.Libs.Publish;

/// <summary>
/// SlowCow publishing service.
/// </summary>
public class SlowCowPublish
{
    private readonly IRepo _repository;

    /// <inheritdoc cref="SlowCowPublish"/>
    public SlowCowPublish(IRepo repository, string channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(channel));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        Channel = channel;
    }

    public string Channel { get; }

    /// <summary>
    /// Return list of available versions.
    /// </summary>
    /// <returns>List of available versions.</returns>
    public Task<IReadOnlyCollection<VersionInfo>> GetVersionsAsync() => _repository.GetAvailableVersionsAsync(Channel);

    /// <summary>
    /// Get latest version detailed information.
    /// </summary>
    /// <returns>Latest version information.</returns>
    public Task<DetailedVersionInfo?> GetLatestVersionAsync() => _repository.GetLatestVersionAsync(Channel);

    /// <summary>
    /// Publish a new version of application.
    /// </summary>
    public async Task<bool> PublishVersionAsync(string filesPath, string? releaseNotes, string? version = null)
    {
        // source folder must exist
        if (!Directory.Exists(filesPath))
        {
            Log.Error("Publish canceled. Source folder not found");
            return false;
        }

        // prepare version
        if (string.IsNullOrWhiteSpace(version))
        {
            var latestVersionInfo = await _repository.GetLatestVersionAsync(Channel);
            if (string.IsNullOrWhiteSpace(latestVersionInfo?.Version))
            {
                version = "1.0.0";
            }
            else
            {
                var lastVersion = new Version(latestVersionInfo.Version);
                version = $"{lastVersion.Major}.{lastVersion.Minor}.{lastVersion.Build + 1}";
            }
        }
        Log.Information("Publish version: {Version}", version);

        // pack files
        var latestHashes = await _repository.GetLatestFullHashesAsync(Channel) ?? new ();
        Log.Information("Packing files from {Path}", filesPath);
        using var packed = await Packer.PackAsync(filesPath, latestHashes);
        if (packed == null)
        {
            Log.Error("Publish canceled. Pack failed");
            return false;
        }

        // upload release
        var release = new UploadReleaseInfo {
            Version = new VersionInfo {
                Channel = Channel,
                Version = version,
            },
            ReleaseNotes = releaseNotes,
            FullPackage = packed.FullPack,
            DiffPackage = packed.DiffPack,
        };

        return await _repository.UploadReleaseAsync(release);
    }
}
