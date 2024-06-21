using System.Reflection;
using Newtonsoft.Json;
using SlowCow.Apps.Shared.Services;
namespace SlowCow.Apps.Publisher.Services;

public static class SettingsService
{
    public static async Task<T> ReadSettingsAsync<T>(string filePath, ArgsService? args = null)
    {
        if (!File.Exists(filePath))
        {
            Log.Error("Cannot find settings file ({FilePath})", filePath);
            throw new FileNotFoundException("Cannot find settings file", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            Log.Error("Settings file is empty ({FilePath})", filePath);
            throw new FileLoadException("Settings file is empty", filePath);
        }

        try
        {
            var result = JsonConvert.DeserializeObject<T>(json);
            if (result == null)
            {
                Log.Error("Failed to deserialize settings file ({FilePath})", filePath);
                throw new FileLoadException("Failed to deserialize settings file", filePath);
            }

            return ApplyArgs(args, result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to deserialize settings file ({FilePath})", filePath);
            throw;
        }
    }

    private static T ApplyArgs<T>(ArgsService? args, T result)
    {
        if (args is not { RawCommands.Count: > 0 }) return result;

        foreach (var (key, value) in args.RawCommands)
        {
            var property = typeof(T).GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (property == null) continue;

            property.SetValue(result, value);
        }

        return result;
    }
}
