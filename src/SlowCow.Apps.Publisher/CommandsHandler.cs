using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using SlowCow.Apps.Shared;
using SlowCow.Apps.Shared.Services;
using SlowCow.Libs.Publish;
namespace SlowCow.Examples.Publisher;

public static class CommandsHandler
{
    public static Task HandleAsync(ArgsService args, PublishSettings settings, SlowCowPublish publishService)
    {
        if (args.ContainsFlag("list")) return ListReleasesAsync(publishService);
        if (args.ContainsFlag("latest")) return ShowLatestAsync(publishService);
        if (args.ContainsFlag("publish")) return PublishAsync(publishService, settings);

        // print help
        Log.Information("Commands:");
        Log.Information("  --list: List all versions");
        Log.Information("  --latest: Show latest version");
        Log.Information("  --publish: Publish new version");

        return Task.CompletedTask;
    }

    public static bool ReadSettings<TRepoSettings>(ArgsService args, [NotNullWhen(true)] out PublishSettings<TRepoSettings>? settings)
    {
        settings = null;
        if (!args.TryGetValue("settings", out var settingsFile))
        {
            Log.Error("run with --settings <fullPathToSettingsFile>");
            return false;
        }

        if (!File.Exists(settingsFile))
        {
            Log.Error("Settings file not found: {File}", settingsFile);
            return false;
        }

        try
        {
            var settingsJson = File.ReadAllText(settingsFile);
            settings = JsonConvert.DeserializeObject<PublishSettings<TRepoSettings>>(settingsJson);
            if (settings == null) return false;
        }
        catch
        {
            Log.Error("Failed to read settings file: {File}", settingsFile);
            return false;
        }

        if (args.TryGetValue("notes", out var releaseNotes))
            settings = settings with { ReleaseNotes = releaseNotes };

        if (args.TryGetValue("version", out var version))
            settings = settings with { Version = version };

        return true;
    }

    private static async Task PublishAsync(SlowCowPublish publishService, PublishSettings settings)
    {
        Log.Information("Publishing {Version} ({Channel})...", settings.Version ?? "auto-version", settings.Channel);
        if (!Directory.Exists(settings.Path))
        {
            Log.Error("Source folder not found: {Path}", settings.Path);
            return;
        }

        if (!File.Exists(settings.SetupExecutableFullPath))
        {
            Log.Error("Setup path not found: {Setup}", settings.SetupExecutableFullPath);
            return;
        }

        // include setup to publish folder
        var setupFileName = Constants.SetupFileNameWithoutExtension + Path.GetExtension(settings.SetupExecutableFullPath);
        var setupFilePath = Path.Combine(settings.Path, setupFileName);
        Log.Information("Copying setup file to publish folder... ({SetupFilePath})", setupFilePath);
        File.Copy(settings.SetupExecutableFullPath, setupFilePath, true);

        // publish
        if (await publishService.PublishVersionAsync(settings.Path, settings.ReleaseNotes, settings.Version))
        {
            Log.Information("Publishing completed");
        }
        else
        {
            Log.Error("Publishing failed");
        }
    }

    private static async Task ShowLatestAsync(SlowCowPublish publishService)
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

    private static async Task ListReleasesAsync(SlowCowPublish publishService)
    {
        var versions = await publishService.GetVersionsAsync();
        foreach (var version in versions)
            Log.Information("  {VersionVersion}-{Channel}", version.Version, version.Channel);

        if (versions.Count == 0) Log.Information("  - No versions found");
    }
}
