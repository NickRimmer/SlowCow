using Serilog;
namespace SlowCow.Apps.Shared.Services;

public static class LoggerService
{
    public static void Init(string? logFilePath = null)
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console();

        if (!string.IsNullOrWhiteSpace(logFilePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
            configuration.WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day);
        }

        Log.Logger = configuration
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Log.Error(eventArgs.ExceptionObject as Exception, "Unhandled exception");
            Log.CloseAndFlush();
        };
    }
}
