using Microsoft.Extensions.Logging;
using SlowCow.Setup.Repo.Base.Models;
namespace SlowCow.Setup.Repo.Base.Interfaces;

public interface IRepo
{
    Task<RepoReleaseModel?> GetLastReleaseAsync(string channel, ILogger logger);
    Task<bool> UploadAsync(RepoReleaseModel releaseInfo, string sourcePath, ILogger logger);
    Task<DownloadResultModel?> DownloadAsync(string channel, string loadVersion, Dictionary<string, string> installedHashes, ILogger logger);
}
