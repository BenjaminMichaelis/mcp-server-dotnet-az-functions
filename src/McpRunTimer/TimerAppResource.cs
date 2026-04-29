using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace McpRunTimer;

/// <summary>
/// MCP App resource that serves the timer UI widget.
/// This file is the "wow moment" addition during the talk —
/// adding this + [McpMetadata] on GetElapsed turns a text tool
/// into an interactive UI rendered inside the chat.
/// </summary>
public class TimerAppResource(ILogger<TimerAppResource> logger)
{
    [Function(nameof(GetTimerWidget))]
    public string GetTimerWidget(
        [McpResourceTrigger(
            "ui://timer/index.html",
            "Run Timer Widget",
            MimeType = "text/html;profile=mcp-app",
            Description = "Interactive run timer display for MCP Apps")]
            ResourceInvocationContext context)
    {
        logger.LogInformation("Timer widget resource invoked.");
        var file = Path.Combine(AppContext.BaseDirectory, "app", "dist", "index.html");
        return File.ReadAllText(file);
    }
}
