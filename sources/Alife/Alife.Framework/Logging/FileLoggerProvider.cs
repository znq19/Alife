using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.Extensions.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    readonly string _logDirectory;
    readonly string _filePrefix;
    readonly ConcurrentQueue<string> _logQueue = new();
    readonly Timer _flushTimer;
    bool _disposed;

    public LogLevel MinLevel { get; set; } = LogLevel.Information;

    public FileLoggerProvider(string logDirectory, string filePrefix = "", int flushIntervalMs = 1000)
    {
        _logDirectory = logDirectory;
        _filePrefix = string.IsNullOrEmpty(filePrefix)
            ? ""
            : string.Concat(filePrefix.Split(Path.GetInvalidFileNameChars()));
        Directory.CreateDirectory(_logDirectory);

        string fileName = string.IsNullOrEmpty(_filePrefix) ? "app.log" : $"{_filePrefix}.log";
        File.WriteAllText(Path.Combine(_logDirectory, fileName), "");

        _flushTimer = new Timer(_ => Flush(), null, flushIntervalMs, flushIntervalMs);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal void WriteLog(string logLine)
    {
        _logQueue.Enqueue(logLine);
    }

    void Flush()
    {
        if (_logQueue.IsEmpty) return;

        string fileName = string.IsNullOrEmpty(_filePrefix) ? "app.log" : $"{_filePrefix}.log";
        string logFile = Path.Combine(_logDirectory, fileName);
        using StreamWriter writer = new(logFile, append: true, encoding: Encoding.UTF8);
        while (_logQueue.TryDequeue(out string? logLine))
        {
            writer.WriteLine(logLine);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _flushTimer.Dispose();
        Flush();
    }
}
