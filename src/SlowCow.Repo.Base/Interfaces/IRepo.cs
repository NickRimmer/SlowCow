using SlowCow.Repo.Base.Models;
namespace SlowCow.Repo.Base.Interfaces;

/// <summary>
/// SlowCow data repository interface.
/// </summary>
public interface IRepo
{
    /// <summary>
    /// Get all available versions.
    /// </summary>
    /// <param name="channel">Channel to filter by. Stay null or empty to get all.</param>
    /// <returns>List of versions.</returns>
    Task<IReadOnlyCollection<VersionInfo>> GetAvailableVersionsAsync(string? channel);

    /// <summary>
    /// Get detailed information about latest version.
    /// </summary>
    /// <param name="channel">Channel name.</param>
    /// <returns>Returns null when no versions published in channel.</returns>
    Task<DetailedVersionInfo?> GetLatestVersionAsync(string channel);

    /// <summary>
    /// Return latest hashes of files in repository.
    /// </summary>
    /// <param name="channel">Channel name.</param>
    /// <returns>Dictionary where key is relative file path and value hash string. Returns null when no versions found for channel.</returns>
    Task<Dictionary<string, string>?> GetLatestFullHashesAsync(string channel);

    /// <summary>
    /// Return latest hashes of files different from previous release in repository.
    /// </summary>
    /// <param name="channel">Channel name.</param>
    /// <returns>Dictionary where key is relative file path and value hash string. Returns null when no versions found for channel.</returns>
    Task<Dictionary<string, string>?> GetLatestDiffHashesAsync(string channel);

    /// <summary>
    /// Get package stream by version.
    /// </summary>
    /// <param name="version">Version name.</param>
    /// <param name="channel">Channel name.</param>
    /// <param name="full">Download full package or only changed files from previous version.</param>
    /// <returns>Returns a stream to read package data.</returns>
    Task<PackedStreamInfo?> DownloadPackageStreamAsync(string version, string channel, bool full);

    /// <summary>
    /// Upload new release to repository.
    /// </summary>
    /// <param name="releaseInfo">Release details.</param>
    /// <returns>Returns true if success.</returns>
    Task<bool> UploadReleaseAsync(UploadReleaseInfo releaseInfo);

    /// <summary>
    /// Delete version from repository.
    /// </summary>
    /// <param name="version">Version name.</param>
    /// <param name="channel">Channel name.</param>
    /// <returns>Returns true when deleted or doesn't exist already</returns>
    Task<bool> DeleteReleaseAsync(string version, string channel);
}
