using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace McpRunTimer;

public class RunTimerResource(ILogger<RunTimerResource> logger)
{
    [Function(nameof(GetTimerStatus))]
    public string GetTimerStatus(
        [McpResourceTrigger(
            "timer://status",
            "Timer Status",
            Description = "Returns all active and recent timers.",
            MimeType = "application/json")]
            ResourceInvocationContext context)
    {
        logger.LogInformation("Timer status resource invoked.");

        var timers = RunTimerTools.Timers.Values
            .OrderByDescending(t => t.StartedAtUtc)
            .Take(10)
            .Select(t => new
            {
                timerId = t.Id,
                state = t.IsRunning ? "running" : "completed",
                elapsed = FormatDuration(t.Elapsed),
                startedAt = t.StartedAtUtc.ToString("O"),
                completedAt = t.CompletedAtUtc?.ToString("O")
            });

        return JsonSerializer.Serialize(new
        {
            timers,
            checkedAt = DateTime.UtcNow.ToString("O")
        });
    }

    private static string FormatDuration(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
            : $"{ts.TotalSeconds:F1}s";
}
