using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
namespace SlowCow.Setup.Base.Loggers;

public class TempLogger : ILogger
{
    private static readonly object Lock = new ();
    private readonly Guid _appId;
    private readonly ConsoleLogger _consoleLogger;

    public TempLogger(Guid appId)
    {
        _appId = appId;
        _consoleLogger = new ConsoleLogger();
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // duplicate log to console
        _consoleLogger.Log(logLevel, eventId, state, exception, formatter);

        var now = DateTime.Now;
        var fileName = $"{now:yyyy-MM-dd}.txt";
        var filePath = Path.Combine(Path.GetTempPath(), _appId.ToString(), fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        lock (Lock)
        {
            var message = $"[{now:HH:mm:ss}] {logLevel}: {formatter(state, exception)}{Environment.NewLine}";
            if (exception != null)
            {
                message += $"{exception.Message}{Environment.NewLine}";
                message += $"{exception.StackTrace}{Environment.NewLine}";
            }
            File.AppendAllText(filePath, message);
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    // ReSharper disable once HeuristicUnreachableCode
    [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
    #pragma warning disable CS0162 // Unreachable code detected
    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel >= LogLevel.Information) return true;

        #if DEBUG
        return true;
        #endif
        return false;
    }
    #pragma warning restore CS0162 // Unreachable code detected
}
