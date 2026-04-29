using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace McpRunTimer;

public class RunTimerResource(ILogger<RunTimerResource> logger)
{
    [Function(nameof(GetRunStatus))]
    public string GetRunStatus(
        [McpResourceTrigger(
            "run://status",
            "Run Status",
            Description = "Returns the current run timer state including elapsed time.",
            MimeType = "application/json")]
            ResourceInvocationContext context)
    {
        logger.LogInformation("Run status resource invoked.");

        var startTime = RunTimerTools.StartTimeUtc;
        var endTime = RunTimerTools.EndTimeUtc;

        string state;
        string? elapsed = null;

        if (startTime is null)
        {
            state = "idle";
        }
        else if (endTime is null)
        {
            state = "running";
            elapsed = FormatDuration(DateTime.UtcNow - startTime.Value);
        }
        else
        {
            state = "completed";
            elapsed = FormatDuration(endTime.Value - startTime.Value);
        }

        return JsonSerializer.Serialize(new
        {
            state,
            elapsed,
            startedAt = startTime?.ToString("O"),
            completedAt = endTime?.ToString("O"),
            checkedAt = DateTime.UtcNow.ToString("O")
        });
    }

    private static string FormatDuration(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
            : $"{ts.TotalSeconds:F1}s";
}
