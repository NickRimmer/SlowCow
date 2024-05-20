using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;
using SlowCow.Setup.Base.Interfaces;
using SlowCow.Setup.Repo.Base.Exceptions;
using SlowCow.Setup.Repo.Base.Interfaces;
using SlowCow.Setup.Repo.Base.Models;
using SlowCow.Setup.Services;
using SlowCow.Setup.UI;
namespace SlowCow.Setup;

public static class Runner
{
    private static IServiceProvider ServicesInDesign { get; } = new ServiceCollection()
        .AddSingleton(new RunnerSettingsModel {
            Name = string.Empty,
            Description = string.Empty,
            ApplicationId = Guid.Empty,
            ExecutableFileName = "Example.App.exe",
            Channel = RepoReleaseModel.DefaultChannel,
        })
        .AddSingleton<IRepo>(new InDesignRepo())
        .BuildServiceProvider();

    internal static IServiceProvider Services { get; private set; } = Design.IsDesignMode ? ServicesInDesign : null!;

    public static async Task RunAsync(RunnerSettingsModel runnerSettings, IRepo setup, IInstaller installer, ILogger? customLogger = null)
    {
        // catch all unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var logger = Services.GetRequiredService<ILogger>();
            logger.LogError((Exception) args.ExceptionObject, "Unhandled exception");
        };

        var args = Environment.GetCommandLineArgs();

        if (TryGetArgsValue(args, "channel", out var channel))
            runnerSettings = runnerSettings with { Channel = channel };

        if (TryGetArgsValue(args, "parent", out var parentProcessId))
            runnerSettings = runnerSettings with { ParentProcessId = parentProcessId };

        runnerSettings = runnerSettings with {
            HasRepairFlag = ArgsHasFlag(args, "repair"),
        };

        Services = new ServiceCollection()
            .AddSingleton(customLogger ?? new Base.Loggers.ConsoleLogger())
            .AddSingleton(setup)
            .AddSingleton(installer)
            .AddSingleton<UpdatesInfoService>()
            .AddSingleton(runnerSettings)
            .BuildServiceProvider();

        var logger = Services.GetRequiredService<ILogger>();
        logger.LogDebug("Runner started with arguments: {Join}", string.Join(" ", args));

        if (ArgsHasFlag(args, "uninstall"))
        {
            logger.LogInformation("Uninstalling...");
            RunUninstall();
            return;
        }

        if (ArgsHasFlag(args, "uninstall-cleaning") && !string.IsNullOrWhiteSpace(runnerSettings.ParentProcessId))
        {
            logger.LogInformation("Cleaning after uninstall...");
            var installationPath = installer.InstallationPath;
            RunUninstallCleaning(runnerSettings.ParentProcessId, installationPath);
            return;
        }

        if (ArgsHasFlag(args, "self-update") && !string.IsNullOrWhiteSpace(runnerSettings.ParentProcessId))
        {
            logger.LogInformation("Self-updating...");
            var installationPath = installer.InstallationPath;
            RunSelfUpdate(runnerSettings.ParentProcessId, installationPath);
            return;
        }

        if (TryGetArgsValue(args, "upload", out var packSettingsFile))
        {
            logger.LogInformation("Uploading...");
            if (!File.Exists(packSettingsFile)) throw new RepoException($"Settings file '{packSettingsFile}' does not exist.");
            await RunUploadAsync(packSettingsFile);
            return;
        }

        if (TryGetArgsValue(args, "get-update", out var versionFileName))
        {
            logger.LogInformation("Getting update information...");
            await RunGetUpdateAsync(versionFileName);
            return;
        }

        logger.LogInformation("Running setup...");
        RunSetup();
        logger.LogInformation("Setup exit");
    }

    private static bool TryGetArgsValue(IEnumerable<string> args, string name, [NotNullWhen(true)] out string? value)
    {
        var arg = args.FirstOrDefault(x => x.StartsWith($"--{name}=", StringComparison.OrdinalIgnoreCase));
        if (arg == null)
        {
            value = null;
            return false;
        }

        value = arg.Split('=')[1].Trim('"', '\'');
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool ArgsHasFlag(IEnumerable<string> args, string name) => args.Any(x => x.Equals($"--{name}", StringComparison.OrdinalIgnoreCase));

    private static void RunUninstallCleaning(string processId, string cleaningFolder)
    {
        // wait for the process to exit
        var logger = Services.GetRequiredService<ILogger>();
        try
        {
            logger.LogInformation("Waiting for the process {ProcessId} to exit...", processId);
            var process = Process.GetProcessById(int.Parse(processId));
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            // ignored
            logger.LogError(ex, "Error while waiting for the process to exit");
        }

        // delete the folder
        var leftTries = 20;
        while (leftTries-- > 0)
        {
            try
            {
                Directory.Delete(cleaningFolder, true);
                break;
            }
            catch
            {
                Task.Delay(3000).GetAwaiter().GetResult();
            }
        }

        logger.LogInformation("Folder '{CleaningFolder}' deleted successfully", cleaningFolder);
    }

    private static void RunSelfUpdate(string processId, string targetFolder)
    {
        // wait for the process to exit
        var logger = Services.GetRequiredService<ILogger>();
        try
        {
            logger.LogInformation("Waiting for the process {ProcessId} to exit...", processId);
            var process = Process.GetProcessById(int.Parse(processId));
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            // ignored
            logger.LogError(ex, "Error while waiting for the process to exit");
        }

        // copy itself to the target folder
        var sourceFile = Process.GetCurrentProcess().MainModule?.FileName;
        if (sourceFile == null) return;

        var targetFile = Path.Combine(targetFolder, $"setup{Path.GetExtension(sourceFile)}"); // TODO I don't like it...
        logger.LogInformation("Copying '{SourceFile}' to '{TargetFile}'", sourceFile, targetFile);
        File.Copy(sourceFile, targetFile, true);
    }

    private static Task<bool> RunUploadAsync(string settingsFile)
    {
        AppBuilder
            .Configure<Dummy>()
            .UsePlatformDetect()
            .LogToTrace(LogEventLevel.Verbose);

        var settingsJson = File.ReadAllText(settingsFile);
        var settings = JsonConvert.DeserializeObject<RepoReleaseModelExt>(settingsJson) ?? throw new RepoException("Failed to read settings from JSON");
        if (string.IsNullOrWhiteSpace(settings.Channel)) settings = settings with { Channel = RepoReleaseModel.DefaultChannel };

        var repo = Services.GetRequiredService<IRepo>();
        var logger = Services.GetRequiredService<ILogger>();
        Services.GetRequiredService<ILogger>().LogInformation("Uploading '{SourcePath}' with repo '{Name}'", settings.SourcePath, repo.GetType().Name);
        return repo.UploadAsync(settings, settings.SourcePath, logger);
    }

    private static void RunUninstall()
    {
        AppBuilder
            .Configure<Dummy>()
            .UsePlatformDetect()
            .AfterSetup(_ => Task.Run(async () =>
            {
                var installer = Services.GetRequiredService<IInstaller>();
                var runnerSettings = Services.GetRequiredService<RunnerSettingsModel>();
                var logger = Services.GetRequiredService<ILogger>();

                Services.GetRequiredService<ILogger>().LogDebug("Uninstalling with installer {Name}", installer.GetType().Name);
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var result = await MessageBoxManager.GetMessageBoxStandard(runnerSettings.Name, "Are you sure you want to uninstall the application?", ButtonEnum.YesNo, Icon.Question).ShowAsync();
                    if (result != ButtonResult.Yes)
                    {
                        logger.LogInformation("Uninstall cancelled by user");
                        return;
                    }

                    await installer.UninstallAsync(Services.GetRequiredService<ILogger>());
                });
                Environment.Exit(0);
            }).ConfigureAwait(false))
            .StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
    }

    private static async Task RunGetUpdateAsync(string filePath)
    {
        var logger = Services.GetRequiredService<ILogger>();
        try
        {
            var versionInfo = await Services.GetRequiredService<UpdatesInfoService>().GetInfoAsync();
            var versionJson = JsonConvert.SerializeObject(versionInfo, Formatting.Indented);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(filePath, versionJson);
            logger.LogInformation("File saved to: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while getting version");
        }
    }

    private static void RunSetup()
    {
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(LogEventLevel.Verbose)
            .UseReactiveUI()
            .StartWithClassicDesktopLifetime([]);
    }

    private record RepoReleaseModelExt : RepoReleaseModel
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public required string SourcePath { get; init; }
    }

    private class InDesignRepo : IRepo
    {
        public Task<RepoReleaseModel?> GetLastReleaseAsync(string channel, ILogger logger) => throw new NotImplementedException();
        public Task<bool> UploadAsync(RepoReleaseModel releaseInfo, string sourcePath, ILogger logger) => throw new NotImplementedException();
        public Task<DownloadResultModel?> DownloadAsync(string channel, string loadVersion, Dictionary<string, string> installedHashes, ILogger logger) => throw new NotImplementedException();
    }
}
