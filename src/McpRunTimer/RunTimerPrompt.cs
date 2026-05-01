using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace McpRunTimer;

public class RunTimerPrompt(ILogger<RunTimerPrompt> logger)
{
    [Function(nameof(TimerCoach))]
    public string TimerCoach(
        [McpPromptTrigger("timer_coach", Description = "A helpful assistant that uses the timer tools to track time for any activity.")]
            PromptInvocationContext context,
        [McpPromptArgument("activity", "What you are timing (e.g., 'a workout', 'cooking pasta', 'a meeting').")]
            string? activity)
    {
        logger.LogInformation("Timer coach prompt invoked with activity: {Activity}", activity);

        var activityInstruction = activity is not null
            ? $"The user is timing: **{activity}**. Tailor your updates to this activity."
            : "The user hasn't specified what they are timing. Help them track time for whatever they need.";

        return $"""
            You are a helpful assistant that tracks time for activities.

            {activityInstruction}

            You have access to these tools:
            - **start_timer**  Start the timer
            - **get_elapsed**  Check how long has elapsed
            - **stop_timer**  Stop the timer and get the total time

            Guidelines:
            - Call start_timer when the user is ready to begin.
            - Use get_elapsed to give updates when asked.
            - Call stop_timer when the user is done.
            """;
    }
}
