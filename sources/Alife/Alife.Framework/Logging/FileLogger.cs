using System.Collections.Concurrent;
using System.Text;

using System;

namespace Microsoft.Extensions.Logging;

public sealed class FileLogger(string categoryName, FileLoggerProvider provider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= provider.MinLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string message = formatter(state, exception);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string level = logLevel.ToString().ToUpper().PadRight(7);
        string logLine = $"[{timestamp}] [{level}] [{categoryName}] {message}";
        if (exception != null)
            logLine += $"\n{exception}";

        provider.WriteLog(logLine);
    }
}
