using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace McpRunTimer;

public sealed class TimerState
{
    public string Id { get; }
    public string SessionId { get; }
    public Stopwatch Stopwatch { get; } = new();
    public DateTime StartedAtUtc { get; }
    public DateTime? CompletedAtUtc { get; private set; }

    public TimerState(string id, string sessionId)
    {
        Id = id;
        SessionId = sessionId;
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
    internal static readonly ConcurrentDictionary<string, TimerState> Timers = new();

    [Function(nameof(OpenTimer))]
    public string OpenTimer(
        [McpToolTrigger("open_timer", "Opens the timer widget. Use start_timer, get_elapsed, and stop_timer to control it.")]
            ToolInvocationContext context)
    {
        return "{\"state\":\"idle\"}";
    }

    [Function(nameof(StartTimer))]
    public string StartTimer(
        [McpToolTrigger("start_timer", "Starts a new timer and returns the timer ID.")]
            ToolInvocationContext context)
    {
        var sessionId = context.SessionId ?? "";
        var id = Guid.NewGuid().ToString("N")[..8];
        var timer = new TimerState(id, sessionId);
        Timers[id] = timer;

        logger.LogInformation("Timer {TimerId} started (session {SessionId}).", id, sessionId);

        return $"Timer started at {timer.StartedAtUtc:HH:mm:ss} UTC. Your timer ID is: {id}";
    }

    [Function(nameof(GetElapsed))]
    public string GetElapsed(
        [McpToolTrigger("get_elapsed", "Returns the elapsed time for a timer.")]
            ToolInvocationContext context,
        [McpToolProperty("timer_id", "The timer ID returned by start_timer.", true)]
            string timerId)
    {
        if (!Timers.TryGetValue(timerId, out var timer))
        {
            return $"No timer found with ID '{timerId}'. Use start_timer first.";
        }

        var sessionId = context.SessionId ?? "";
        if (timer.SessionId != sessionId)
        {
            return $"No timer found with ID '{timerId}'. Use start_timer first.";
        }

        var elapsed = timer.Elapsed;
        logger.LogInformation("Timer {TimerId} elapsed: {Elapsed}", timerId, elapsed);

        return JsonSerializer.Serialize(new
        {
            timerId = timer.Id,
            state = timer.IsRunning ? "running" : "completed",
            elapsed = FormatDuration(elapsed),
            elapsedSeconds = elapsed.TotalSeconds,
            startedAt = timer.StartedAtUtc.ToString("O")
        });
    }

    [Function(nameof(StopTimer))]
    public string StopTimer(
        [McpToolTrigger("stop_timer", "Stops a timer and returns the total elapsed time.")]
            ToolInvocationContext context,
        [McpToolProperty("timer_id", "The timer ID returned by start_timer.", true)]
            string timerId)
    {
        if (!Timers.TryGetValue(timerId, out var timer))
        {
            return $"No timer found with ID '{timerId}'. Use start_timer first.";
        }

        var sessionId = context.SessionId ?? "";
        if (timer.SessionId != sessionId)
        {
            return $"No timer found with ID '{timerId}'. Use start_timer first.";
        }

        if (!timer.IsRunning)
        {
            return $"Timer '{timerId}' is already stopped. Total: {FormatDuration(timer.Elapsed)}";
        }

        timer.Stop();
        logger.LogInformation("Timer {TimerId} stopped. Total: {Elapsed}", timerId, timer.Elapsed);

        return $"""
            Timer complete!
            Timer ID: {timer.Id}
            Started:  {timer.StartedAtUtc:HH:mm:ss} UTC
            Stopped:  {timer.CompletedAtUtc:HH:mm:ss} UTC
            Total:    {FormatDuration(timer.Elapsed)}
            """;
    }

    [Function(nameof(GetSessionTimers))]
    public string GetSessionTimers(
        [McpToolTrigger("get_session_timers", "Returns all timers started in the current session.")]
            ToolInvocationContext context)
    {
        var sessionId = context.SessionId ?? "";

        var sessionTimers = Timers.Values
            .Where(t => t.SessionId == sessionId)
            .OrderByDescending(t => t.StartedAtUtc)
            .Select(t => new
            {
                timerId = t.Id,
                state = t.IsRunning ? "running" : "completed",
                elapsed = FormatDuration(t.Elapsed),
                elapsedSeconds = t.Elapsed.TotalSeconds,
                startedAt = t.StartedAtUtc.ToString("O")
            });

        return JsonSerializer.Serialize(new { timers = sessionTimers });
    }

    private static string FormatDuration(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
            : $"{ts.TotalSeconds:F1}s";
}
