using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SlowCow.Setup.Modules.Installers;
using SlowCow.Setup.Modules.Installers.Base;
using SlowCow.Setup.Modules.Runner;
using SlowCow.Setup.Modules.Setups.Base;
using SlowCow.Setup.Modules.Setups.Base.Exceptions;
using SlowCow.Setup.Modules.Setups.LocalSetup;
using SlowCow.Setup.Modules.Updates;
using SlowCow.Setup.UI;
using SlowCow.Shared;
namespace SlowCow.Setup;

public static class Runner
{
    private static IServiceProvider ServicesInDesign { get; } = new ServiceCollection()
        .AddSingleton(new RunnerModel {
            Name = string.Empty,
            Description = string.Empty,
            ApplicationId = Guid.Empty,
            ExecutableFileName = "Example.App.exe",
        })
        .AddSingleton<ISetup>(new LocalSetup("in-design"))
        .BuildServiceProvider();

    public static IServiceProvider Services { get; private set; } = Design.IsDesignMode ? ServicesInDesign : null!;

    public static async Task RunAsync(RunnerModel runnerSettings, ISetup setup)
    {
        var args = Environment.GetCommandLineArgs();

        if (TryGetArgsValue(args, Constants.SetupArgNameChannel, out var channel))
            runnerSettings = runnerSettings with { Channel = channel };

        if (TryGetArgsValue(args, Constants.SetupArgNameParentProcessId, out var parentProcessId))
            runnerSettings = runnerSettings with { ParentProcessId = parentProcessId };

        runnerSettings = runnerSettings with {
            HasRepairFlag = ArgsHasFlag(args, "repair"),
        };

        Services = new ServiceCollection()
            .AddSingleton(setup)
            .AddSingleton<InstallerProvider>()
            .AddSingleton<IInstaller, WindowsInstaller>()
            .AddSingleton<UpdatesInfoService>()
            .AddSingleton(runnerSettings)
            .BuildServiceProvider();

        if (ArgsHasFlag(args, "uninstall"))
        {
            RunUninstall();
            return;
        }

        if (TryGetArgsValue(args, "uninstall-cleaning", out var values))
        {
            var valuesArray = values.Split(';');
            var processId = valuesArray[0];
            var cleaningFolder = valuesArray[1];

            RunUninstallCleaning(processId, cleaningFolder);
            return;
        }

        if (TryGetArgsValue(args, "pack", out var packSettingsFile))
        {
            if (!File.Exists(packSettingsFile)) throw new PackerException($"Settings file '{packSettingsFile}' does not exist.");
            await RunPackAsync(packSettingsFile);
            return;
        }

        if (TryGetArgsValue(args, Constants.SetupArgNameGetVersion, out var versionFileName))
        {
            await RunGetVersionAsync(versionFileName);
            return;
        }

        RunSetup();
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
        try
        {
            var process = Process.GetProcessById(int.Parse(processId));
            process?.WaitForExit();
        }
        catch
        {
            // ignored
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
    }

    private static Task RunPackAsync(string settingsFile)
    {
        var settingsJson = File.ReadAllText(settingsFile);
        return Services
            .GetRequiredService<ISetup>()
            .PackAsync(settingsJson);
    }

    private static void RunUninstall()
    {
        AppBuilder
            .Configure<Dummy>()
            .UsePlatformDetect()
            .AfterSetup(_ => Task.Run(async () =>
            {
                var installer = Services
                    .GetRequiredService<InstallerProvider>()
                    .GetInstaller();

                await Dispatcher.UIThread.InvokeAsync(installer.UninstallAsync);
                Environment.Exit(0);
            }).ConfigureAwait(false))
            .StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
    }

    private static async Task RunGetVersionAsync(string filePath)
    {
        try
        {
            var versionInfo = await Services.GetRequiredService<UpdatesInfoService>().GetInfoAsync();
            var versionJson = JsonConvert.SerializeObject(versionInfo, Formatting.Indented);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(filePath, versionJson);
            Console.WriteLine($"File saved to: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    private static void RunSetup()
    {
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI()
            .StartWithClassicDesktopLifetime([]);
    }
}
