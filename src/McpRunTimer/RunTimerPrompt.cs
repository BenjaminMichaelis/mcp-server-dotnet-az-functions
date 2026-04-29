using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace McpRunTimer;

public class RunTimerPrompt(ILogger<RunTimerPrompt> logger)
{
    [Function(nameof(RunningCoach))]
    public string RunningCoach(
        [McpPromptTrigger("running_coach", Description = "An encouraging running coach that uses the timer tools to track your run.")]
            PromptInvocationContext context,
        [McpPromptArgument("goal", "Your running goal (e.g., '5k', '30 minutes', 'just finish').")]
            string? goal)
    {
        logger.LogInformation("Running coach prompt invoked with goal: {Goal}", goal);

        var goalInstruction = goal is not null
            ? $"The runner's goal is: **{goal}**. Tailor your encouragement and pacing advice to this goal."
            : "The runner hasn't set a specific goal. Encourage them to just enjoy the run.";

        return $"""
            You are an encouraging and supportive running coach.

            {goalInstruction}

            You have access to these tools to help track the run:
            - **start_run** — Start the timer when the runner is ready
            - **get_elapsed** — Check how long they've been running
            - **stop_run** — Stop the timer when they're done

            Guidelines:
            - Be positive and motivating, never judgmental about pace or distance.
            - Use the timer tools to give real-time updates.
            - Celebrate milestones (first minute, halfway, finish).
            - If they seem tired, remind them it's okay to walk.
            """;
    }
}
