using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SlowCow.Apps.Shared;
namespace SlowCow.Examples.Launcher.Windows;

public static class LaunchHandler
{
    public static void Launch(string installationPath, string[]? args = null)
    {
        Log.Information("Launching application");

        var currentPath = Path.Combine(installationPath, Constants.CurrentFolderName);
        var executablePath = GetExecutablePath(currentPath);

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            Log.Error("Executable not found ({ExecutablePath})", executablePath);
            return;
        }

        Log.Information("Executable path: {ExecutablePath}", executablePath);

        // run process and exit
        var processSettings = new ProcessStartInfo {
            FileName = executablePath,
            WorkingDirectory = currentPath,
            UseShellExecute = false,
        };

        if (args is { Length: > 0 })
            processSettings.Arguments = string.Join(' ', args);

        var process = new Process {
            StartInfo = processSettings,
        };

        var started = process.Start();
        if (started)
        {
            Log.Information("Process started ({ProcessId})", process.Id);
        }
        else
        {
            Log.Error("Failed to start process ({ExecutablePath})", executablePath);
        }
    }

    private static string GetExecutablePath(string currentPath)
    {
        var releaseInfoPath = Path.Combine(currentPath, Constants.ReleaseInfoFileName);

        if (!File.Exists(releaseInfoPath))
        {
            Log.Error("Release info file not found ({FilePath})", releaseInfoPath);
            return string.Empty;
        }

        var releaseInfoJson = File.ReadAllText(releaseInfoPath);
        var releaseInfo = JsonConvert.DeserializeObject<JObject>(releaseInfoJson);

        var executableName = releaseInfo?["executableRelativePath"]?.Value<string>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(executableName)) return string.Empty;

        return Path.Combine(currentPath, executableName);
    }
}
