using Newtonsoft.Json;
using Serilog;
using SlowCow.Apps.InstallerBuilder.Models;
using SlowCow.Apps.Shared.Services;
namespace SlowCow.Apps.InstallerBuilder.Services;

public static class AppSettingsService
{
    private const string SettingsFileName = "builder-settings.json5";

    public static Task<AppSettingsModel> LoadAsync(ArgsService args)
    {
        if (args.TryGetValue("settings", out var builderSettingsPath)) return LoadAsync(builderSettingsPath);
        return LoadAsync(Path.Combine(Directory.GetCurrentDirectory(), SettingsFileName));
    }

    private static async Task<AppSettingsModel> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.Error("Settings file not found: {FilePath}", filePath);
            throw new FileNotFoundException("Settings file not found", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath);
        var result = JsonConvert.DeserializeObject<AppSettingsModel>(json);
        if (result != null) return result;

        Log.Error("Failed to deserialize settings file: {FilePath}", filePath);
        throw new Exception("Failed to deserialize settings file");
    }
}
