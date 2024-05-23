using SlowCow.Apps.Shared.Services;
using SlowCow.Examples.Publisher;
using SlowCow.Libs.Publish;
using SlowCow.Repo.GitHub;

const string gitHubEnvironmentVariableName = "SlowCow_GHPAT";

LoggerService.Init();

// read arguments from command line
var argsService = new ArgsService(args);
Log.Debug("Arguments:\n\t{Arguments}", string.Join("\n\t", argsService.RawCommands.Select(x => $"{x.Key}={x.Value}")));

if (!CommandsHandler.ReadSettings<GitHubSettings>(argsService, out var settings))
    return;

var repoSettings = settings.RepoSettings;
var envToken = Environment.GetEnvironmentVariable(gitHubEnvironmentVariableName);
if (!string.IsNullOrWhiteSpace(envToken))
{
    Log.Debug("Using GitHub token from environment variable");
    repoSettings = repoSettings with { AccessToken = envToken };
}
else
{
    Log.Debug("GitHub token not found in environment variable ({VariableName})", gitHubEnvironmentVariableName);
}

// configure services
var repo = new GitHubRepo(repoSettings);
var publishService = new SlowCowPublish(repo, settings.Channel);

// run commands
await CommandsHandler.HandleAsync(argsService, settings, publishService);
Log.CloseAndFlush();