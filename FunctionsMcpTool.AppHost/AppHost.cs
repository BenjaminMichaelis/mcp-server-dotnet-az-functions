var builder = DistributedApplication.CreateBuilder(args);

const string McpEndpointPath = "/runtime/webhooks/mcp";
const int McpInspectorClientPort = 6284;
const int McpInspectorServerPort = 6287;

// UI build paths
var showcaseAppUiPath = Path.Combine("..", "src", "FunctionsMcpTool", "app");
var showcaseWeatherUiPath = Path.Combine("..", "src", "FunctionsMcpTool", "app-weather");
var timerAppUiPath = Path.Combine("..", "src", "McpRunTimer", "app");

// ─── Showcase server UI builds ─────────────────────────────────────
var showcaseAppUiBuild = builder.AddJavaScriptApp("showcase-app-ui-build", showcaseAppUiPath, "build")
    .WithNpm(installCommand: "ci", installArgs: ["--no-audit", "--no-fund"]);

var showcaseAppUiWatch = builder.AddJavaScriptApp("showcase-app-ui-watch", showcaseAppUiPath, "build:watch")
    .WithNpm(install: false)
    .WaitForCompletion(showcaseAppUiBuild);

var showcaseWeatherUiBuild = builder.AddJavaScriptApp("showcase-weather-ui-build", showcaseWeatherUiPath, "build")
    .WithNpm(installCommand: "ci", installArgs: ["--no-audit", "--no-fund"]);

var showcaseWeatherUiWatch = builder.AddJavaScriptApp("showcase-weather-ui-watch", showcaseWeatherUiPath, "build:watch")
    .WithNpm(install: false)
    .WaitForCompletion(showcaseWeatherUiBuild);

// ─── Timer server UI build ─────────────────────────────────────────
var timerAppUiBuild = builder.AddJavaScriptApp("timer-app-ui-build", timerAppUiPath, "build")
    .WithNpm(installCommand: "ci", installArgs: ["--no-audit", "--no-fund"]);

var timerAppUiWatch = builder.AddJavaScriptApp("timer-app-ui-watch", timerAppUiPath, "build:watch")
    .WithNpm(install: false)
    .WaitForCompletion(timerAppUiBuild);

// ─── Function App projects ─────────────────────────────────────────
var mcpRunTimer = builder.AddAzureFunctionsProject<Projects.McpRunTimer>("mcp-run-timer")
    .WithExternalHttpEndpoints()
    .WaitForCompletion(timerAppUiBuild)
    .WaitForStart(timerAppUiWatch);

var functionsMcpTool = builder.AddAzureFunctionsProject<Projects.FunctionsMcpTool>("functions-mcp-tool")
    .WithExternalHttpEndpoints()
    .WaitForCompletion(showcaseAppUiBuild)
    .WaitForStart(showcaseAppUiWatch)
    .WaitForCompletion(showcaseWeatherUiBuild)
    .WaitForStart(showcaseWeatherUiWatch);

// Parent relationships for dashboard grouping
timerAppUiBuild.WithParentRelationship(mcpRunTimer);
timerAppUiWatch.WithParentRelationship(mcpRunTimer);
showcaseAppUiBuild.WithParentRelationship(functionsMcpTool);
showcaseAppUiWatch.WithParentRelationship(functionsMcpTool);
showcaseWeatherUiBuild.WithParentRelationship(functionsMcpTool);
showcaseWeatherUiWatch.WithParentRelationship(functionsMcpTool);

// ─── MCP Inspector ─────────────────────────────────────────────────
builder.AddMcpInspector("mcp-inspector", options =>
    {
        options.ClientPort = McpInspectorClientPort;
        options.ServerPort = McpInspectorServerPort;
        options.InspectorVersion = "0.21.2";
    })
    .WithMcpServer(mcpRunTimer, isDefault: true, path: McpEndpointPath)
    .WithMcpServer(functionsMcpTool, isDefault: false, path: McpEndpointPath)
    .WithUrls(context =>
    {
        foreach (var url in context.Urls)
        {
            var isClientUrl = string.Equals(url.DisplayText, "Client", StringComparison.Ordinal) ||
                url.Url.Contains($":{McpInspectorClientPort}/", StringComparison.Ordinal);

            if (!isClientUrl ||
                url.Url.Contains("MCP_PROXY_PORT=", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = url.Url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            url.Url = $"{url.Url}{separator}MCP_PROXY_PORT={McpInspectorServerPort}";
        }

        return Task.CompletedTask;
    });

builder.Build().Run();
