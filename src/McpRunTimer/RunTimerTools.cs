using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace McpRunTimer;

public sealed class RunState
{
    public string Id { get; }
    public Stopwatch Stopwatch { get; } = new();
    public DateTime StartedAtUtc { get; }
    public DateTime? CompletedAtUtc { get; private set; }

    public RunState(string id)
    {
        Id = id;
        StartedAtUtc = DateTime.UtcNow;
        Stopwatch.Start();
    }

    public void Stop()
    {
        Stopwatch.Stop();
        CompletedAtUtc = DateTime.UtcNow;
    }

    public bool IsRunning => Stopwatch.IsRunning;
    public TimeSpan Elapsed => Stopwatch.Elapsed;
}

public class RunTimerTools(ILogger<RunTimerTools> logger)
{
    internal static readonly ConcurrentDictionary<string, RunState> Runs = new();

    [Function(nameof(StartRun))]
    public string StartRun(
        [McpToolTrigger("start_run", "Starts tracking your run time. Call this when you begin a run.")]
            ToolInvocationContext context)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var run = new RunState(id);
        Runs[id] = run;

        logger.LogInformation("Run {RunId} started.", id);

        return $"Timer started at {run.StartedAtUtc:HH:mm:ss} UTC. Your run ID is: {id}";
    }

    [Function(nameof(GetElapsed))]
    public string GetElapsed(
        [McpToolTrigger("get_elapsed", "Returns how long the current run has been going.")]
            ToolInvocationContext context,
        [McpToolProperty("run_id", "The run ID returned by start_run.", true)]
            string runId)
    {
        if (!Runs.TryGetValue(runId, out var run))
        {
            return $"No run found with ID '{runId}'. Use start_run to begin tracking.";
        }

        var elapsed = run.Elapsed;
        logger.LogInformation("Run {RunId} elapsed: {Elapsed}", runId, elapsed);

        return JsonSerializer.Serialize(new
        {
            runId = run.Id,
            state = run.IsRunning ? "running" : "completed",
            elapsed = FormatDuration(elapsed),
            elapsedSeconds = elapsed.TotalSeconds,
            startedAt = run.StartedAtUtc.ToString("O")
        });
    }

    [Function(nameof(StopRun))]
    public string StopRun(
        [McpToolTrigger("stop_run", "Stops the run timer and returns your total time.")]
            ToolInvocationContext context,
        [McpToolProperty("run_id", "The run ID returned by start_run.", true)]
            string runId)
    {
        if (!Runs.TryGetValue(runId, out var run))
        {
            return $"No run found with ID '{runId}'. Use start_run first.";
        }

        if (!run.IsRunning)
        {
            return $"Run '{runId}' is already stopped. Total: {FormatDuration(run.Elapsed)}";
        }

        run.Stop();
        logger.LogInformation("Run {RunId} stopped. Total: {Elapsed}", runId, run.Elapsed);

        return $"""
            Run complete!
            Run ID:   {run.Id}
            Started:  {run.StartedAtUtc:HH:mm:ss} UTC
            Stopped:  {run.CompletedAtUtc:HH:mm:ss} UTC
            Total:    {FormatDuration(run.Elapsed)}
            """;
    }

    private static string FormatDuration(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
            : $"{ts.TotalSeconds:F1}s";
}
