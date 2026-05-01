var builder = DistributedApplication.CreateBuilder(args);

const string McpEndpointPath = "/runtime/webhooks/mcp";
const int McpInspectorTimerClientPort = 6284;
const int McpInspectorTimerServerPort = 6287;
const int McpInspectorShowcaseClientPort = 6285;
const int McpInspectorShowcaseServerPort = 6288;

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
var funcStorage = builder.AddAzureStorage("func-storage").RunAsEmulator();

var mcpRunTimer = builder.AddAzureFunctionsProject<Projects.McpRunTimer>("mcp-run-timer")
    .WithHostStorage(funcStorage)
    .WithHostStorage(funcStorage)
    .WithExternalHttpEndpoints()
    .WaitForCompletion(timerAppUiBuild)
    .WaitForStart(timerAppUiWatch);

var functionsMcpTool = builder.AddAzureFunctionsProject<Projects.FunctionsMcpTool>("functions-mcp-tool")
    .WithHostStorage(funcStorage)
    .WithHostStorage(funcStorage)
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

// ─── MCP Inspectors (one per server) ──────────────────────────────
builder.AddMcpInspector("mcp-inspector-timer", options =>
    {
        options.ClientPort = McpInspectorTimerClientPort;
        options.ServerPort = McpInspectorTimerServerPort;
        options.InspectorVersion = "0.21.2";
    })
    .WithMcpServer(mcpRunTimer, isDefault: true, path: McpEndpointPath)
    .WithUrls(context =>
    {
        foreach (var url in context.Urls)
        {
            var isClientUrl = string.Equals(url.DisplayText, "Client", StringComparison.Ordinal) ||
                url.Url.Contains($":{McpInspectorTimerClientPort}/", StringComparison.Ordinal);

            if (!isClientUrl ||
                url.Url.Contains("MCP_PROXY_PORT=", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = url.Url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            url.Url = $"{url.Url}{separator}MCP_PROXY_PORT={McpInspectorTimerServerPort}";
        }

        return Task.CompletedTask;
    });

builder.AddMcpInspector("mcp-inspector-showcase", options =>
    {
        options.ClientPort = McpInspectorShowcaseClientPort;
        options.ServerPort = McpInspectorShowcaseServerPort;
        options.InspectorVersion = "0.21.2";
    })
    .WithMcpServer(functionsMcpTool, isDefault: true, path: McpEndpointPath)
    .WithUrls(context =>
    {
        foreach (var url in context.Urls)
        {
            var isClientUrl = string.Equals(url.DisplayText, "Client", StringComparison.Ordinal) ||
                url.Url.Contains($":{McpInspectorShowcaseClientPort}/", StringComparison.Ordinal);

            if (!isClientUrl ||
                url.Url.Contains("MCP_PROXY_PORT=", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = url.Url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            url.Url = $"{url.Url}{separator}MCP_PROXY_PORT={McpInspectorShowcaseServerPort}";
        }

        return Task.CompletedTask;
    });

builder.Build().Run();
