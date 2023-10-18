namespace SourceGeneratorTestApi;
public static partial class Log
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Critical,
        Message = "Weather is shit cause of `{reason}`")]
    public static partial void WeatherIsBadCauseOf(this ILogger logger, string reason);
}