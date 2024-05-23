using Newtonsoft.Json;
using SlowCow.Apps.InstallerBase.Services;
using SlowCow.Apps.Shared.Services;
using SlowCow.Installers.Base;
using SlowCow.Installers.Windows;
using SlowCow.Repo.GitHub;

LoggerService.Init();
var argService = new ArgsService(args);
var appSettings = await AppSettingsService.LoadAsync(args);

Log.Debug("Arguments:\n\t{Arguments}", string.Join("\n\t", argService.RawCommands.Select(x => $"{x.Key}={x.Value}")));
if (argService.ContainsFlag("--debug"))
    Log.Debug("AppSettings:\n{AppSettings}", JsonConvert.SerializeObject(appSettings, Formatting.Indented));

var repo = new GitHubRepo(appSettings.RepoSettings);
var installer = new WindowsInstaller(appSettings.InstallationSettings);

if (argService.ContainsFlag("uninstall"))
{
    Log.Information("Uninstall application '{AppName}' with id {Id}", appSettings.InstallationSettings.ApplicationName, appSettings.InstallationSettings.ApplicationId);
    await UninstallService.UninstallAsync(installer);
    Log.Information("Uninstall completed");
}
else if (argService.TryGetValue(Constants.UninstallCleanupArgName, out _))
{
    Log.Information("Cleanup after uninstalling started");
    await UninstallService.UninstallCleaningAsync(installer, argService);
    Log.Information("Cleaning up completed");
}
else
{
    Log.Information("Installation started");
    await InstallerService.RunAsync(repo, installer, appSettings);
    Log.Information("Installation completed");
}

Log.Information("Installer exited\n-------------------------");
Log.CloseAndFlush();
