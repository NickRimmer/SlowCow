using SlowCow.Apps.Publisher;
using SlowCow.Apps.Publisher.Models;
using SlowCow.Apps.Publisher.Services;
using SlowCow.Apps.Shared.Services;
using SlowCow.Libs.Publish;
using SlowCow.Repo.GitHub;

LoggerService.Init();
var argsService = new ArgsService(args);

// publish command
if (argsService.TryGetValue("publish", out var publishSettingsFilePath))
{
    if (argsService.ContainsFlag("help"))
    {
        Log.Information("Commands:");
        Log.Information("  --list: List all versions");
        Log.Information("  --latest: Show latest version");
        Log.Information("  --publish: Publish new version");
        return;
    }

    // read settings
    var publishSettings = await SettingsService.ReadSettingsAsync<PublishSettingsModel<GitHubSettings>>(publishSettingsFilePath, argsService);
    var repo = new GitHubRepo(publishSettings.RepoSettings);
    var publishService = new SlowCowPublish(repo, publishSettings.Channel);

    if (argsService.ContainsFlag("list")) await PublishService.ListReleasesAsync(publishService);
    else if (argsService.ContainsFlag("latest")) await PublishService.ShowLatestAsync(publishService);
    else await PublishService.PublishAsync(publishSettings, publishService);
}

// build command
else if (argsService.TryGetValue("build", out var buildSettingsFilePath))
{
    var buildSettings = await SettingsService.ReadSettingsAsync<BuildSettingsModel>(buildSettingsFilePath, argsService);
    await BuildService.BuildAsync(buildSettings);
}

// show help
else
{
    Log.Information("Commands:");
    Log.Information("  --publish <publish-settings.json> <settings>: Publish new version. Settings are JSON settings properties overriding (e.g. --releaseNotes 'New bugs added')");
    Log.Information("  --publish <publish-settings.json> <command>: Show published information");
    Log.Information("  --publish --help: Show help for publish command");
    Log.Information("  --build <publish-settings.json>: Build new version");
}