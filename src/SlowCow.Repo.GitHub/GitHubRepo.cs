using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Octokit;
using Serilog;
using SlowCow.Repo.Base.Exceptions;
using SlowCow.Repo.Base.Interfaces;
using SlowCow.Repo.Base.Models;
using JsonException = System.Text.Json.JsonException;
using ProductHeaderValue = Octokit.ProductHeaderValue;
namespace SlowCow.Repo.GitHub;

/// <summary>
/// SlowCow GitHub data repository.
/// </summary>
public class GitHubRepo : IRepo
{
    private const string DefaultReleaseNotesMessage = "No release notes provided.";
    private const string VersionTagNameRegex = @"v(?<version>\d+\.\d+\.\d+)-(?<channel>[\w-]+)";
    private const string VersionTagNameTemplate = "v{version}-{channel}";
    private const string ReleaseFullPackAssetName = "pack-full";
    private const string ReleaseDiffPackAssetName = "pack-diff";

    private readonly GitHubSettings _settings;
    private readonly GitHubClient _ghClient;
    private readonly HttpClient _httpClient;

    /// <inheritdoc cref="GitHubRepo" />
    public GitHubRepo(GitHubSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Debug.Assert(!string.IsNullOrWhiteSpace(_settings.Owner));
        Debug.Assert(!string.IsNullOrWhiteSpace(_settings.RepositoryName));

        _ghClient = CreateGhClient();
        _httpClient = CreateHttpClient();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<VersionInfo>> GetAvailableVersionsAsync(string? channel)
    {
        Log.Debug("Getting available versions from GitHub ({Channel})", channel);

        var ghReleases = await GetGhReleasesAsync();
        var result = ghReleases
            .Select(x =>
            {
                var match = Regex.Match(x.TagName, VersionTagNameRegex);
                if (!match.Success) return null;

                return new VersionInfo {
                    Version = match.Groups["version"].Value,
                    Channel = match.Groups["channel"].Value,
                };
            })
            .Where(x => x != null)
            .Where(x => string.IsNullOrWhiteSpace(channel) || x!.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Log.Debug("Got {Count} versions from GitHub", result.Count);
        return result!;
    }

    /// <inheritdoc />
    public async Task<DetailedVersionInfo?> GetLatestVersionAsync(string channel)
    {
        Log.Debug("Getting latest version from GitHub ({Channel})", channel);

        var releases = await GetGhReleasesAsync();
        var latest = releases
            .Select(x => new {
                Release = x,
                VersionMatch = Regex.Match(x.TagName, VersionTagNameRegex),
            })
            .Where(x => x.VersionMatch.Success)
            .Select(x => new {
                x.Release,
                Version = new Version(x.VersionMatch.Groups["version"].Value),
                Channel = x.VersionMatch.Groups["channel"].Value,
            })
            .MaxBy(x => x.Version);


        if (latest == null)
        {
            Log.Debug("No versions found in channel {Channel}", channel);
            return null;
        }

        var result = new DetailedVersionInfo {
            Version = latest.Version.ToString(),
            Channel = latest.Channel,
            ReleaseNotes = latest.Release.Body ?? string.Empty,
            PublishedAt = latest.Release.PublishedAt!.Value,
        };

        Log.Debug("Got latest version from GitHub ({Version})", result.Version);
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> UploadReleaseAsync(UploadReleaseInfo releaseInfo)
    {
        Log.Information("Uploading pack for version {Version} ({Owner}/{RepoName})", releaseInfo.Version, _settings.Owner, _settings.RepositoryName);

        if (string.IsNullOrWhiteSpace(releaseInfo.ReleaseNotes)) releaseInfo = releaseInfo with { ReleaseNotes = DefaultReleaseNotesMessage };
        var releaseTag = VersionTagNameTemplate
            .Replace("{version}", releaseInfo.Version.Version)
            .Replace("{channel}", releaseInfo.Version.Channel);

        // create draft release
        var release = new NewRelease(releaseTag) {
            Name = releaseTag,
            Draft = true,
            Prerelease = false,
            Body = releaseInfo.ReleaseNotes,
        };

        var releaseResult = await _ghClient.Repository.Release.Create(_settings.Owner, _settings.RepositoryName, release);
        if (releaseResult == null)
        {
            Log.Error("Failed to create release");
            return false;
        }

        await UploadAssetAsync(releaseResult, $"{ReleaseFullPackAssetName}.zip", releaseInfo.FullPackage.TempPath);
        await UploadAssetAsync(releaseResult, $"{ReleaseFullPackAssetName}.json", JsonConvert.SerializeObject(releaseInfo.FullPackage.Hashes, Formatting.Indented));

        if (releaseInfo.DiffPackage?.Hashes.Count > 0 && !string.IsNullOrWhiteSpace(releaseInfo.DiffPackage.TempPath) && File.Exists(releaseInfo.DiffPackage.TempPath))
        {
            await UploadAssetAsync(releaseResult, $"{ReleaseDiffPackAssetName}.zip", releaseInfo.DiffPackage.TempPath);
            await UploadAssetAsync(releaseResult, $"{ReleaseDiffPackAssetName}.json", JsonConvert.SerializeObject(releaseInfo.DiffPackage.Hashes, Formatting.Indented));
        }

        // update release draft status
        var releaseUpdate = releaseResult.ToUpdate();
        releaseUpdate.Draft = false;
        var updateResult = await _ghClient.Repository.Release.Edit(_settings.Owner, _settings.RepositoryName, releaseResult.Id, releaseUpdate);

        if (updateResult == null)
        {
            Log.Warning("Failed to update release");
            return false;
        }

        Log.Information("Release uploaded successfully. Version: {Version}, Channel: {Channel}", releaseInfo.Version.Version, releaseInfo.Version.Channel);
        return true;
    }

    /// <inheritdoc />
    public async Task<PackedStreamInfo?> DownloadPackageStreamAsync(string version, string channel, bool downloadFullPackage)
    {
        var release = await GetGhReleaseAsync(version, channel);
        if (release == null) return null;

        var hashesAssetId = downloadFullPackage
            ? release.Assets.FirstOrDefault(x => x.Name.Equals($"{ReleaseFullPackAssetName}.json", StringComparison.OrdinalIgnoreCase))?.Id
            : release.Assets.FirstOrDefault(x => x.Name.Equals($"{ReleaseDiffPackAssetName}.json", StringComparison.OrdinalIgnoreCase))?.Id;

        var packAssetId = downloadFullPackage
            ? release.Assets.FirstOrDefault(x => x.Name.Equals($"{ReleaseFullPackAssetName}.zip", StringComparison.OrdinalIgnoreCase))?.Id
            : release.Assets.FirstOrDefault(x => x.Name.Equals($"{ReleaseDiffPackAssetName}.zip", StringComparison.OrdinalIgnoreCase))?.Id;

        var hashes = await GetHashesAsync(hashesAssetId) ?? new ();
        var packStream = await GetPackStreamAsync(packAssetId);
        if (packStream is not { CanRead: true })
        {
            Log.Warning("Failed to download pack. Download stream is empty");
            return null;
        }

        return new PackedStreamInfo(packStream, hashes, downloadFullPackage);
    }

    /// <inheritdoc />
    public Task<bool> DeleteReleaseAsync(string version, string channel) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Dictionary<string, string>?> GetLatestFullHashesAsync(string channel) => GetLatestHashesAsync(channel, ReleaseFullPackAssetName);

    /// <inheritdoc />
    public Task<Dictionary<string, string>?> GetLatestDiffHashesAsync(string channel) => GetLatestHashesAsync(channel, ReleaseDiffPackAssetName);

    private async Task<Dictionary<string, string>?> GetLatestHashesAsync(string channel, string fileName)
    {
        Log.Debug("Getting hashes {FileName} from GitHub ({Channel})", fileName, channel);

        var releases = await GetGhReleasesAsync();
        var latest = releases.Select(x => new {
                Release = x,
                Match = Regex.Match(x.TagName, VersionTagNameRegex),
            })
            .FirstOrDefault(x => x.Match.Success);

        if (latest == null)
        {
            Log.Debug("No versions found in channel {Channel}", channel);
            return null;
        }

        var hashesAssetId = latest.Release.Assets.FirstOrDefault(x => x.Name.Equals($"{fileName}.json", StringComparison.OrdinalIgnoreCase))?.Id;
        return await GetHashesAsync(hashesAssetId);
    }

    private async Task UploadAssetAsync(Release releaseResult, string fileName, string payload)
    {
        Log.Information("Uploading asset '{FileName}' ...", fileName);
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
            Log.Information("Asset uploaded successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to upload asset '{FileName}'", fileName);
            throw;
        }
    }

    private async Task<Release?> GetGhReleaseAsync(string version, string channel)
    {
        var tag = VersionTagNameTemplate
            .Replace("{version}", version)
            .Replace("{channel}", channel);

        var release = await _ghClient.Repository.Release.Get(_settings.Owner, _settings.RepositoryName, tag);
        return release;
    }

    private async Task<Stream?> GetPackStreamAsync(int? assetId)
    {
        if (!assetId.HasValue) return null;
        var assetUrl = $"https://api.github.com/repos/{_settings.Owner}/{_settings.RepositoryName}/releases/assets/{assetId}";
        Log.Information("Pack download URL: {AssetUrl}", assetUrl);

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
            Log.Error("Failed to download pack from '{AssetUrl}'", assetUrl);
            throw;
        }
    }

    private async Task<Dictionary<string, string>?> GetHashesAsync(int? assetId)
    {
        if (!assetId.HasValue) return null;
        var assetUrl = $"https://api.github.com/repos/{_settings.Owner}/{_settings.RepositoryName}/releases/assets/{assetId}";
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
                    if (!string.IsNullOrWhiteSpace(responseContent)) Log.Error("Response:\n{Response}", responseContent);
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
            Log.Error("Failed to parse asset '{AssetUrl}' JSON", assetUrl);
            if (!string.IsNullOrWhiteSpace(assetJson)) Log.Error("Response JSON:\n{AssetJson}", assetJson);
            throw;
        }
        catch
        {
            Log.Error("Failed to download asset '{AssetUrl}'", assetUrl);
            throw;
        }
    }

    private async Task<IEnumerable<Release>> GetGhReleasesAsync()
    {
        var ghReleases = await _ghClient.Repository.Release.GetAll(_settings.Owner, _settings.RepositoryName);
        return ghReleases
            .OrderByDescending(x => x.TagName)
            .Where(x => x.PublishedAt.HasValue && !x.Draft);
    }

    private GitHubClient CreateGhClient()
    {
        var client = new GitHubClient(new ProductHeaderValue(_settings.Owner));
        if (!string.IsNullOrWhiteSpace(_settings.AccessToken))
            client.Credentials = new Credentials(_settings.AccessToken);

        return client;
    }

    private HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        var assemblyVersion = GetType().Assembly.GetName().Version;
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SlowCow", assemblyVersion?.ToString() ?? "0.0.0"));

        if (!string.IsNullOrWhiteSpace(_settings.AccessToken))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

        return client;
    }
}
