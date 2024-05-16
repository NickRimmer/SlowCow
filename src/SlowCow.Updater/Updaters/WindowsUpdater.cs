using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json;
using SlowCow.Shared;
using SlowCow.Updater.Common;
namespace SlowCow.Updater.Updaters;

internal class WindowsUpdater: IUpdater
{
    public SlowCowVersion? GetVersion()
    {
        // by default, the setup file is located in the parent directory
        var installationPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..");
        var setupPath = Path.Combine(installationPath, Constants.WindowsSetupFileName);

        if (!File.Exists(setupPath))
            throw new SlowCowException($"The setup file is missing. ({setupPath})");

        var versionOutputFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + ".json");
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = setupPath,
                Arguments = $"--{Constants.SetupArgNameGetVersion}={versionOutputFilePath}",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        process.WaitForExit();

        // parse JSON
        try
        {
            var json = File.ReadAllText(versionOutputFilePath);
            return JsonConvert.DeserializeObject<SlowCowVersion>(json);
        }
        catch
        {
            throw new SlowCowException("No version information found");
        }
    }
}
