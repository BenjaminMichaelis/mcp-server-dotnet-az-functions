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
            Description = "Returns all active and recent run timers.",
            MimeType = "application/json")]
            ResourceInvocationContext context)
    {
        logger.LogInformation("Run status resource invoked.");

        var runs = RunTimerTools.Runs.Values
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(10)
            .Select(r => new
            {
                runId = r.Id,
                state = r.IsRunning ? "running" : "completed",
                elapsed = FormatDuration(r.Elapsed),
                startedAt = r.StartedAtUtc.ToString("O"),
                completedAt = r.CompletedAtUtc?.ToString("O")
            });

        return JsonSerializer.Serialize(new
        {
            runs,
            checkedAt = DateTime.UtcNow.ToString("O")
        });
    }

    private static string FormatDuration(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
            : $"{ts.TotalSeconds:F1}s";
}
