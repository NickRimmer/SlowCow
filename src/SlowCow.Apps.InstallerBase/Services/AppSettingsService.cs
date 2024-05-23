using System.Text;
using Newtonsoft.Json;
using SlowCow.Apps.InstallerBase.Models;
using SlowCow.Apps.Shared;
namespace SlowCow.Apps.InstallerBase.Services;

public static class AppSettingsService
{
    // can be used to load app settings from file during development
    private const string DevConfigFileName = "installer-settings.json";

    public static async Task<AppSettingsModel> LoadAsync(string[] args)
    {
        var installerPath = Environment.ProcessPath!;
        var appSettingsJson = await TryReadFromExecutableAsync(installerPath);

        // if not settings embedded in executable, try to read from file
        if (string.IsNullOrWhiteSpace(appSettingsJson))
            appSettingsJson = await TryReadFromFileAsync(installerPath);

        // if appSettingsJson is still empty, throw exception
        if (string.IsNullOrWhiteSpace(appSettingsJson)) throw new Exception("Failed to read app settings");

        var result = JsonConvert.DeserializeObject<AppSettingsModel>(appSettingsJson) ?? throw new Exception("Unexpected empty app settings");
        return ApplyArgs(result, args);
    }

    private static AppSettingsModel ApplyArgs(AppSettingsModel appSettings, string[] args)
    {
        //TODO: apply args to appSettings
        return appSettings;
    }

    private static Task<string> TryReadFromFileAsync(string installerPath)
    {
        var configFilePath = Path.Combine(Path.GetDirectoryName(installerPath)!, DevConfigFileName);
        if (File.Exists(configFilePath)) return File.ReadAllTextAsync(configFilePath);

        Log.Error("Cannot read config from installer");
        return Task.FromResult(string.Empty);

    }

    private static async Task<string> TryReadFromExecutableAsync(string installerPath)
    {
        // config is added as JSON in bytes to executable after this delimiter. Extract it and deserialize it as installationSettings
        var installerBytes = await File.ReadAllBytesAsync(installerPath);
        var installerDelimiterBytes = Encoding.UTF8.GetBytes(Constants.InstallerInjectionDelimiter);
        var delimiterIndex = installerBytes
            .Select((_, i) => new {
                Index = i,
                Match = installerBytes.Skip(i).Take(installerDelimiterBytes.Length).SequenceEqual(installerDelimiterBytes),
            })
            .FirstOrDefault(x => x.Match)
            ?.Index ?? -1;

        if (delimiterIndex == -1) return string.Empty;

        try
        {
            var appSettingsBytes = installerBytes.Skip(delimiterIndex + installerDelimiterBytes.Length).ToArray();
            return Encoding.UTF8.GetString(appSettingsBytes);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read app settings from executable. {Message} ({Type})", ex.Message, ex.GetType().Name);
            return string.Empty;
        }
    }
}
