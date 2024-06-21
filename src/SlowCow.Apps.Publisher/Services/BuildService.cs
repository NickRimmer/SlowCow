using System.Text;
using Newtonsoft.Json;
using SlowCow.Apps.Publisher.Models;
using SlowCow.Apps.Shared;
namespace SlowCow.Apps.Publisher.Services;

public static class BuildService
{
    public static Task BuildAsync(BuildSettingsModel settings)
    {
        var copyFullPath = CreateCopy(settings);
        return EmbeddingSettingsAsync(copyFullPath, settings);
    }

    private static async Task EmbeddingSettingsAsync(string copyFullPath, BuildSettingsModel settings)
    {
        var configJson = JsonConvert.SerializeObject(settings.InstallerSettings);
        var configBytes = Encoding.UTF8.GetBytes(configJson);
        var delimiterBytes = Encoding.UTF8.GetBytes(Constants.InstallerInjectionDelimiter);

        await using var installerFile = File.Open(copyFullPath, FileMode.Append);
        await installerFile.WriteAsync(delimiterBytes, 0, delimiterBytes.Length);
        await installerFile.WriteAsync(configBytes, 0, configBytes.Length);

        Log.Information("Settings embedded to {Path}", copyFullPath);
    }

    private static string CreateCopy(BuildSettingsModel settings)
    {
        if (!File.Exists(settings.BaseInstallerPath))
        {
            Log.Error("Base installer not found ({Path})", settings.BaseInstallerPath);
            throw new FileNotFoundException("Base installer not found", settings.BaseInstallerPath);
        }

        var outputName = string.IsNullOrWhiteSpace(settings.OutputName) ? (Path.GetFileNameWithoutExtension(settings.BaseInstallerPath) + "-packed") : settings.OutputName;
        var outputFolder = string.IsNullOrWhiteSpace(settings.OutputFolder) ? Path.GetDirectoryName(settings.BaseInstallerPath)! : settings.OutputFolder;
        var outputFullPath = Path.Combine(outputFolder, outputName + Path.GetExtension(settings.BaseInstallerPath));

        Log.Information("Copying base installer to {Path}", outputFullPath);
        Directory.CreateDirectory(outputFolder);
        File.Copy(settings.BaseInstallerPath, outputFullPath, true);

        return outputFullPath;
    }
}
