using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json;
using SlowCow.Shared;
using SlowCow.Updater.Common;
namespace SlowCow.Updater.Updaters;

internal class WindowsUpdater : IUpdater
{
    public SlowCowVersion? GetVersion()
    {
        // run command
        var outputFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + ".json");
        RunSetup(new Dictionary<string, object> {
            { Constants.SetupArgNameGetVersion, outputFilePath },
        });

        // read output JSON
        try
        {
            var json = File.ReadAllText(outputFilePath);
            return JsonConvert.DeserializeObject<SlowCowVersion>(json);
        }
        catch
        {
            throw new SlowCowException("No version information found");
        }
    }

    public bool InstallLatest()
    {
        var version = GetVersion();
        if (version?.UpdateAvailable != true) return false;

        RunSetup(new Dictionary<string, object> {
            { Constants.SetupArgNameChannel, version.Channel },
            { Constants.SetupArgNameParentProcessId, Environment.ProcessId },
        }, false);

        Environment.Exit(0);
        return true; // will never reach this line :P
    }

    private static void RunSetup(Dictionary<string, object> args, bool waitForExit = true)
    {
        try
        {
            var setupPath = GetSetupPath();
            if (!File.Exists(setupPath))
                throw new SlowCowException($"The setup file is missing. ({setupPath})");

            var argsString = string.Join(" ", args.Select(x => $"--{x.Key}=\"{x.Value}\""));
            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = setupPath,
                    Arguments = argsString,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            if (waitForExit) process.WaitForExit();
        }
        catch (Exception ex)
        {
            throw new SlowCowException($"Failed to run setup. {ex.Message} ({ex.GetType().Name})");
        }
    }

    // by default, the setup file is located in the parent directory
    private static string GetSetupPath() =>
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..", Constants.WindowsSetupFileName);
}
