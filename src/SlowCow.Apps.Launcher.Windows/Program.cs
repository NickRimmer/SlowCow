using SlowCow.Apps.Shared.Services;
using SlowCow.Examples.Launcher.Windows;

// Responsibilities:
// - check if updates folder present, if so, update the application
// - uninstall the application
// - launch the application

LoggerService.Init(Path.Combine(Directory.GetCurrentDirectory(), "logs", "launcher-.log"));
var argService = new ArgsService(args);

// check updates
var executionPath = Path.GetDirectoryName(Environment.ProcessPath)!;

// if uninstall requested, uninstall the application
if (argService.ContainsFlag("uninstall"))
{
    await UninstallHandler.UninstallAsync(executionPath);
    return;
}

// when application uninstalled, delete the installation path (launcher should be started from Temp folder at this moment)
if (argService.TryGetValue("uninstall-complete", out var installationPath))
{
    if (!Directory.Exists(installationPath))
    {
        Log.Error("Installation path does not exist");
        return;
    }

    await UninstallHandler.UninstallCompleteAsync(installationPath);
    return;
}

if (string.IsNullOrWhiteSpace(executionPath))
    throw new InvalidOperationException("Could not determine the execution path.");

if (UpdateHandler.UpdateAvailable(executionPath))
    UpdateHandler.ApplyUpdate(executionPath);

// launch the application
LaunchHandler.Launch(executionPath);
Log.Information("Launcher exited\n-------------------------");
Log.CloseAndFlush();
