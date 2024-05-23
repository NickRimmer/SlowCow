using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Octokit;
using SlowCow.Setup.Repo.Base;
using SlowCow.Setup.Repo.Base.Exceptions;
using SlowCow.Setup.Repo.Base.Interfaces;
using SlowCow.Setup.Repo.Base.Models;
namespace SlowCow.Setup.Repo.GitHub;

public class GitHubRepo : IRepo
{
    private const string VersionTagNameTemplate = @"v(?<version>\d+\.\d+\.\d+)-(?<channel>[\w-]+)";
    private const string TagTemplate = "v{0}-{1}";
    private const string DefaultReleaseNotesMessage = "No release notes provided.";
    private const string ReleaseFullPackAssetName = "pack-full";
    private const string ReleaseDiffPackAssetName = "pack-diff";

    private readonly string _owner;
    private readonly string _repoName;
    private readonly GitHubClient _ghClient;
    private readonly HttpClient _httpClient;
    private RepoReleaseModel? _cachedManifest;

    public GitHubRepo(string owner, string repoName, string token)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(owner));
        if (string.IsNullOrWhiteSpace(repoName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(repoName));

        _owner = owner;
        _repoName = repoName;

        _ghClient = new GitHubClient(new Octokit.ProductHeaderValue(owner));
        if (!string.IsNullOrWhiteSpace(token)) _ghClient.Credentials = new Credentials(token);

        _httpClient = new HttpClient();
        var assemblyVersion = GetType().Assembly.GetName().Version;
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SlowCow.Setup", assemblyVersion?.ToString() ?? "0.0.0"));

        if (_ghClient.Credentials is { } credentials)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Password);
    }

    public async Task<RepoReleaseModel?> GetLastReleaseAsync(string channel, ILogger logger)
    {
        if (_cachedManifest != null) return _cachedManifest;

        logger.LogInformation("Loading last release information...");
        var lastRelease = (await GetReleasesAsync(channel)).LastOrDefault();
        if (lastRelease == null) return null;

        _cachedManifest = new RepoReleaseModel {
            Channel = channel,
            Version = lastRelease.Version.Version,
            ReleaseNotes = new RepoReleaseModel.ReleaseNotesModel {
                Link = lastRelease.Release.HtmlUrl,
                Text = string.IsNullOrWhiteSpace(lastRelease.Release.Body) ? DefaultReleaseNotesMessage : lastRelease.Release.Body,
            },
        };

        logger.LogInformation("Last release information loaded. Version: {Version}, Channel: {Channel}", _cachedManifest.Version, _cachedManifest.Channel);
        return _cachedManifest;
    }

    public async Task<DownloadResultModel?> DownloadAsync(string channel, string loadVersion, Dictionary<string, string> installedHashes, ILogger logger)
    {
        logger.LogInformation("Downloading pack for version {Version}...", loadVersion);

        var releases = await GetReleasesAsync(channel);
        var release = releases.FirstOrDefault(x => x.Version.Version.Equals(loadVersion, StringComparison.OrdinalIgnoreCase));
        if (release == null) return null;

        var releaseHashesId = release.Release.Assets.FirstOrDefault(x => x.Name.Equals($"{ReleaseFullPackAssetName}.json", StringComparison.OrdinalIgnoreCase))?.Id;
        var releaseHashes = await DownloadFullPackHashesAsync(releaseHashesId, logger) ?? new ();
        var actualDiffHashes = releaseHashes.Where(x => !installedHashes.TryGetValue(x.Key, out var hash) && hash?.Equals(x.Value, StringComparison.Ordinal) != true).ToDictionary(x => x.Key, x => x.Value);
        var diffHashesId = release.Release.Assets.FirstOrDefault(x => x.Name.Equals($"{ReleaseDiffPackAssetName}.json", StringComparison.OrdinalIgnoreCase))?.Id;
        var diffHashes = await DownloadFullPackHashesAsync(diffHashesId, logger) ?? new ();
        var diffPack = actualDiffHashes.All(actualDiff => diffHashes.ContainsKey(actualDiff.Key));

        var packAssetId = diffPack
            ? release.Release.Assets.FirstOrDefault(x => x.Name.Equals($"{ReleaseDiffPackAssetName}.zip", StringComparison.OrdinalIgnoreCase))?.Id
            : release.Release.Assets.FirstOrDefault(x => x.Name.Equals($"{ReleaseFullPackAssetName}.zip", StringComparison.OrdinalIgnoreCase))?.Id;

        logger.LogInformation("Download full pack: {}", !diffPack);
        var packStream = await DownloadPackAsync(packAssetId, logger);
        if (packStream == null)
        {
            logger.LogWarning("Failed to download pack. Download stream is empty");
            return null;
        }

        return new DownloadResultModel {
            PackStream = packStream,
            Hashes = releaseHashes,
        };
    }

    public async Task<bool> UploadAsync(RepoReleaseModel releaseInfo, string sourcePath, ILogger logger)
    {
        // build version
        var releases = await GetReleasesAsync(releaseInfo.Channel);
        var lastRelease = releases.LastOrDefault();

        var version = releaseInfo.Version;
        if (string.IsNullOrWhiteSpace(version))
        {
            logger.LogInformation("Version not provided. Incrementing last release version...");
            version = string.IsNullOrWhiteSpace(lastRelease?.Version.Version) ? "1.0.0" : IncrementVersion(lastRelease.Version.Version);
        }

        releaseInfo = releaseInfo with { Version = version };
        logger.LogInformation("Uploading pack for version {Version} ({Owner}/{RepoName})", releaseInfo.Version, _owner, _repoName);

        var lastReleaseHashesId = lastRelease?.Release.Assets.FirstOrDefault(x => x.Name.Equals($"{ReleaseFullPackAssetName}.json", StringComparison.OrdinalIgnoreCase))?.Id;
        var lastReleaseHashes = await DownloadFullPackHashesAsync(lastReleaseHashesId, logger) ?? new ();

        // pack files
        using var packData = await new Packer(logger).PackAsync(sourcePath, lastReleaseHashes);
        if (packData == null)
        {
            logger.LogWarning("Pack failed. Unexpected empty result");
            return false;
        }

        // create draft release
        var releaseNotes = releaseInfo.ReleaseNotes?.Text;
        var releaseTag = string.Format(TagTemplate, releaseInfo.Version, releaseInfo.Channel);
        if (string.IsNullOrWhiteSpace(releaseNotes)) releaseNotes = DefaultReleaseNotesMessage;

        var release = new NewRelease(releaseTag) {
            Name = releaseTag,
            Draft = true,
            Prerelease = false,
            Body = releaseNotes,
        };

        var releaseResult = await _ghClient.Repository.Release.Create(_owner, _repoName, release);
        if (releaseResult == null)
        {
            logger.LogWarning("Failed to create release");
            return false;
        }

        // upload pack
        var executableSelfPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executableSelfPath))
        {
            logger.LogWarning("Failed to get self path, will try to copy on post setup operation");
            throw new RepoException("Failed to get self path");
        }

        await UploadAssetAsync(releaseResult, Path.GetFileName(executableSelfPath), executableSelfPath, logger);
        await UploadAssetAsync(releaseResult, $"{ReleaseFullPackAssetName}.zip", packData.FullPack.TempFilePath, logger);
        await UploadAssetAsync(releaseResult, $"{ReleaseFullPackAssetName}.json", JsonConvert.SerializeObject(packData.FullPack.Hashes, Formatting.Indented), logger);

        if (packData.DiffPack?.Hashes.Count > 0 && !string.IsNullOrWhiteSpace(packData.DiffPack.TempFilePath) && File.Exists(packData.DiffPack.TempFilePath))
        {
            await UploadAssetAsync(releaseResult, $"{ReleaseDiffPackAssetName}.zip", packData.DiffPack.TempFilePath, logger);
            await UploadAssetAsync(releaseResult, $"{ReleaseDiffPackAssetName}.json", JsonConvert.SerializeObject(packData.DiffPack.Hashes, Formatting.Indented), logger);
        }

        // update release to non-draft
        var releaseUpdate = releaseResult.ToUpdate();
        releaseUpdate.Draft = false;
        var updateResult = await _ghClient.Repository.Release.Edit(_owner, _repoName, releaseResult.Id, releaseUpdate);

        if (updateResult == null)
        {
            logger.LogWarning("Failed to update release");
            return false;
        }

        logger.LogInformation("Release uploaded successfully. Version: {Version}, Channel: {Channel}", releaseInfo.Version, releaseInfo.Channel);
        return true;
    }

    private async Task UploadAssetAsync(Release releaseResult, string fileName, string payload, ILogger logger)
    {
        logger.LogInformation("Uploading asset '{FileName}' ...", fileName);
        try
        {
            var contentType = fileName switch {
                _ when fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) => "application/json",
                _ when fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) => "application/zip",
                _ => "application/octet-stream",
            };

            Stream stream = contentType switch {
                "application/json" => new MemoryStream(Encoding.UTF8.GetBytes(payload)),
                _ => new FileStream(payload, System.IO.FileMode.Open, FileAccess.Read),
            };

            var asset = new ReleaseAssetUpload {
                FileName = fileName,
                ContentType = contentType,
                RawData = stream,
            };

            var result = await _ghClient.Repository.Release.UploadAsset(releaseResult, asset);
            if (result == null) throw new RepoException("Failed to upload release asset.");
            logger.LogInformation("Asset uploaded successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload asset '{FileName}'", fileName);
            throw;
        }
    }

    private async Task<IReadOnlyCollection<ReleaseModel>> GetReleasesAsync(string channel)
    {
        var releases = await _ghClient.Repository.Release.GetAll(_owner, _repoName);
        return releases
            .Where(x => !x.Draft && !x.Prerelease)
            .Select(x => new ReleaseModel(x, ParseVersion(x.TagName)!))
            .Where(x => x.Version?.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(x => x.Release.PublishedAt)
            .ThenBy(x => x.Version.Version)
            .ToList();
    }

    private async Task<Dictionary<string, string>?> DownloadFullPackHashesAsync(int? assetId, ILogger logger)
    {
        if (!assetId.HasValue) return null;
        var assetUrl = $"https://api.github.com/repos/{_owner}/{_repoName}/releases/assets/{assetId}";
        var assetJson = string.Empty;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, assetUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                try
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(responseContent)) logger.LogError("Response:\n{Response}", responseContent);
                }
                catch
                {
                    // ignore
                }
            }

            response.EnsureSuccessStatusCode();
            assetJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(assetJson);
        }
        catch (JsonException)
        {
            logger.LogError("Failed to parse asset '{AssetUrl}' JSON", assetUrl);
            if (!string.IsNullOrWhiteSpace(assetJson)) logger.LogError("Response JSON:\n{AssetJson}", assetJson);
            throw;
        }
        catch
        {
            logger.LogError("Failed to download asset '{AssetUrl}'", assetUrl);
            throw;
        }
    }

    private async Task<Stream?> DownloadPackAsync(int? assetId, ILogger logger)
    {
        if (!assetId.HasValue) return null;
        var assetUrl = $"https://api.github.com/repos/{_owner}/{_repoName}/releases/assets/{assetId}";
        logger.LogInformation("Pack download URL: {AssetUrl}", assetUrl);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, assetUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }
        catch
        {
            logger.LogError("Failed to download pack from '{AssetUrl}'", assetUrl);
            throw;
        }
    }

    private static VersionModel? ParseVersion(string tagName)
    {
        var match = Regex.Match(tagName, VersionTagNameTemplate);
        if (!match.Success) return null;

        return new VersionModel(match.Groups["version"].Value, match.Groups["channel"].Value);
    }

    private static string IncrementVersion(string version)
    {
        var versionParts = version.Split('.');
        if (versionParts.Length != 3) throw new ArgumentException("Invalid version format.", nameof(version));

        var major = int.Parse(versionParts[0]);
        var minor = int.Parse(versionParts[1]);
        var patch = int.Parse(versionParts[2]);

        return $"{major}.{minor}.{patch + 1}";
    }

    private record VersionModel(string Version, string Channel);
    private record ReleaseModel(Release Release, VersionModel Version);
}
