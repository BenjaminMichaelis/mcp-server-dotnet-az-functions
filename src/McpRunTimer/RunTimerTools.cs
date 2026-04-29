using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace McpRunTimer;

public class RunTimerTools(ILogger<RunTimerTools> logger)
{
    private static DateTime? _startTime;
    private static DateTime? _endTime;

    internal static DateTime? StartTimeUtc => _startTime;
    internal static DateTime? EndTimeUtc => _endTime;

    [Function(nameof(StartRun))]
    public string StartRun(
        [McpToolTrigger("start_run", "Starts tracking your run time. Call this when you begin a run.")]
            ToolInvocationContext context)
    {
        logger.LogInformation("Starting run timer.");

        _startTime = DateTime.UtcNow;
        _endTime = null;

        return $"Timer started at {_startTime:HH:mm:ss} UTC. Go run!";
    }

    private const string ElapsedToolMetadata = """
        {
            "ui": {
                "resourceUri": "ui://timer/index.html"
            }
        }
        """;

    [Function(nameof(GetElapsed))]
    public string GetElapsed(
        [McpToolTrigger("get_elapsed", "Returns how long the current run has been going.")]
        [McpMetadata(ElapsedToolMetadata)]
            ToolInvocationContext context)
    {
        if (_startTime is null)
        {
            return "No run in progress. Use start_run to begin tracking.";
        }

        var end = _endTime ?? DateTime.UtcNow;
        var elapsed = end - _startTime.Value;

        logger.LogInformation("Elapsed time: {Elapsed}", elapsed);

        return _endTime is not null
            ? $"Run completed. Total time: {FormatDuration(elapsed)}"
            : $"Running for {FormatDuration(elapsed)}";
    }

    [Function(nameof(StopRun))]
    public string StopRun(
        [McpToolTrigger("stop_run", "Stops the run timer and returns your total time.")]
            ToolInvocationContext context)
    {
        if (_startTime is null)
        {
            return "No run in progress. Use start_run first.";
        }

        _endTime = DateTime.UtcNow;
        var elapsed = _endTime.Value - _startTime.Value;

        logger.LogInformation("Run stopped. Total time: {Elapsed}", elapsed);

        var summary = $"""
            Run complete!
            Started:  {_startTime:HH:mm:ss} UTC
            Stopped:  {_endTime:HH:mm:ss} UTC
            Total:    {FormatDuration(elapsed)}
            """;

        return summary;
    }

    private static string FormatDuration(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
            : $"{ts.TotalSeconds:F1}s";
}
