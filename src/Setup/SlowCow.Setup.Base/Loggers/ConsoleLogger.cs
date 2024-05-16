using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
namespace SlowCow.Setup.Base.Loggers;

public class ConsoleLogger : ILogger
{
    //TODO let's see if it will produce any issues on non-Windows platforms
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    public ConsoleLogger()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !AttachConsole(-1))
                this.LogInformation("Console output to debug session only available");
        }
        catch
        {
            this.LogInformation("Console output to debug session only available");
        }
        finally
        {
            var standardOutput = new StreamWriter(Console.OpenStandardOutput());
            standardOutput.AutoFlush = true;
            Console.SetOut(standardOutput);
        }
    }

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
