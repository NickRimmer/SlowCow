using SlowCow.Apps.Publisher.Models;
using SlowCow.Apps.Shared;
using SlowCow.Libs.Publish;
namespace SlowCow.Apps.Publisher.Services;

public static class PublishService
{
    public static async Task ListReleasesAsync(SlowCowPublish publishService)
    {
        Log.Information("Versions in repository:");

        var versions = await publishService.GetVersionsAsync();
        foreach (var version in versions)
            Log.Information("  {VersionVersion}-{Channel}", version.Version, version.Channel);

        if (versions.Count == 0) Log.Information("  - No versions found");
    }

    public static async Task ShowLatestAsync(SlowCowPublish publishService)
    {
        Log.Information("Latest version in channel ({Channel}):", publishService.Channel);
        var latest = await publishService.GetLatestVersionAsync();

        if (latest == null)
        {
            Log.Information("  - No versions found");
            return;
        }

        Log.Information("  Channel: {LatestChannel}", latest.Channel);
        Log.Information("  Version: {LatestVersion}", latest.Version);
        Log.Information("  Published at: {LatestPublishedAt}", latest.PublishedAt);
        Log.Information("  Release notes: {LatestReleaseNotes}", latest.ReleaseNotes);
    }

    public static async Task PublishAsync(PublishSettingsModel publishSettings, SlowCowPublish publishService)
    {
        Log.Information("Publishing {Version} ({Channel})...", publishSettings.Version ?? "auto-version", publishSettings.Channel);
        if (!Directory.Exists(publishSettings.Path))
        {
            Log.Error("Source folder not found: {Path}", publishSettings.Path);
            throw new FileNotFoundException("Source folder not found", publishSettings.Path);
        }

        if (!File.Exists(publishSettings.SetupExecutableFullPath))
        {
            Log.Error("Setup path not found: {Setup}", publishSettings.SetupExecutableFullPath);
            return;
        }

        // include setup to publish folder
        var setupFileName = Constants.SetupFileNameWithoutExtension + Path.GetExtension(publishSettings.SetupExecutableFullPath);
        var setupFilePath = Path.Combine(publishSettings.Path, setupFileName);
        Log.Information("Copying setup file to publish folder... ({SetupFilePath})", setupFilePath);
        File.Copy(publishSettings.SetupExecutableFullPath, setupFilePath, true);

        // publish
        if (await publishService.PublishVersionAsync(publishSettings.Path, publishSettings.ReleaseNotes, publishSettings.Version))
        {
            Log.Information("Publishing completed");
        }
        else
        {
            Log.Error("Publishing failed");
        }
    }
}
