namespace Microsoft.Extensions.Logging;

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string logDirectory, string filePrefix = "", LogLevel minLevel = LogLevel.Information)
    {
        FileLoggerProvider provider = new(logDirectory, filePrefix) { MinLevel = minLevel };
        builder.AddProvider(provider);
        return builder;
    }
}
