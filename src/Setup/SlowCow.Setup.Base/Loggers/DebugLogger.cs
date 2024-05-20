using Microsoft.Extensions.Logging;
namespace SlowCow.Setup.Base.Loggers;

public class DebugLogger : ILogger
{
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
        Console.ForegroundColor = logLevel switch {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Information => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Critical => ConsoleColor.DarkRed,
            _ => ConsoleColor.White,
        };

        Console.WriteLine($"{logLevel}: {formatter(state, exception)}");
        if (exception != null)
        {
            Console.WriteLine($"{exception.Message}{Environment.NewLine}");
            Console.WriteLine($"{exception.StackTrace}{Environment.NewLine}");
        }

        Console.ResetColor();
    }

    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
