using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SlowCow.Setup.Base.Models;
namespace SlowCow.Setup.Windows.Installer;

public partial class WindowsInstaller
{
    public async Task<ReleaseInfoModel?> GetReleaseInfoAsync(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var releaseInfoPath = GetReleaseInfoPath();
        if (!File.Exists(releaseInfoPath))
        {
            logger.LogInformation("Release info file not found. New installation");
            return null;
        }

        try
        {
            var releaseInfoJson = await File.ReadAllTextAsync(releaseInfoPath);
            return JsonConvert.DeserializeObject<ReleaseInfoModel>(releaseInfoJson);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to read release info file: {Path}", releaseInfoPath);
            return null;
        }
    }
}
