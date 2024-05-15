using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
namespace Example.App;

public static class Updater
{
    public static VersionInfo? GetUpdates()
    {
        // by default, the setup file is located in the parent directory
        var installationPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..");
        var setupPath = Path.Combine(installationPath, "Setup.exe");

        if (!File.Exists(setupPath))
            throw new UpdaterException($"The setup file is missing. ({setupPath})");

        var versionOutputFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + ".json");
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = setupPath,
                Arguments = $"--get-version={versionOutputFilePath}",
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
            return JsonConvert.DeserializeObject<VersionInfo>(json);
        }
        catch
        {
            throw new UpdaterException("No version information found");
        }
    }

    public record VersionInfo(string? InstalledVersion, string? AvailableVersion, bool UpdateAvailable, string Channel);
    public class UpdaterException(string message) : Exception(message);
}
