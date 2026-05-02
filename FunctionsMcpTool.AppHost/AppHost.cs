var builder = DistributedApplication.CreateBuilder(args);
var isPublishMode = builder.ExecutionContext.IsPublishMode;

// Add ACA environment — used by `aspire deploy`, no-op for local `aspire run`
builder.AddAzureContainerAppEnvironment("aca-env");

const string McpEndpointPath = "/runtime/webhooks/mcp";
const int McpRunTimerPort = 63007;
const int FunctionsMcpToolPort = 7071;
const int McpInspectorTimerClientPort = 6284;
const int McpInspectorTimerServerPort = 6287;
const int McpInspectorShowcaseClientPort = 6285;
const int McpInspectorShowcaseServerPort = 6288;

// UI build paths
var showcaseAppUiPath = Path.Combine("..", "src", "FunctionsMcpTool", "app");
var showcaseWeatherUiPath = Path.Combine("..", "src", "FunctionsMcpTool", "app-weather");
var timerAppUiPath = Path.Combine("..", "src", "McpRunTimer", "app");

// ─── Storage ───────────────────────────────────────────────────────
var funcStorage = builder.AddAzureStorage("func-storage").RunAsEmulator();

// ─── mcp-run-timer ────────────────────────────────────────────────
var mcpRunTimer = builder.AddAzureFunctionsProject<Projects.McpRunTimer>("mcp-run-timer")
    .WithHostStorage(funcStorage)
    .WithExternalHttpEndpoints()
    .WithEndpoint("http", endpoint => endpoint.Port = McpRunTimerPort);

if (!isPublishMode)
{
    var timerAppUiBuild = builder.AddJavaScriptApp("timer-app-ui-build", timerAppUiPath, "build")
        .WithNpm(installCommand: "ci", installArgs: ["--no-audit", "--no-fund"]);

    var timerAppUiWatch = builder.AddJavaScriptApp("timer-app-ui-watch", timerAppUiPath, "build:watch")
        .WithNpm(install: false)
        .WaitForCompletion(timerAppUiBuild);

    mcpRunTimer
        .WaitForCompletion(timerAppUiBuild)
        .WaitForStart(timerAppUiWatch);

    timerAppUiBuild.WithParentRelationship(mcpRunTimer);
    timerAppUiWatch.WithParentRelationship(mcpRunTimer);

    builder.AddFunctionsMcpInspector("mcp-inspector-timer", mcpRunTimer,
        McpInspectorTimerClientPort, McpInspectorTimerServerPort, McpEndpointPath);
}

// ─── functions-mcp-tool ───────────────────────────────────────────
var functionsMcpTool = builder.AddAzureFunctionsProject<Projects.FunctionsMcpTool>("functions-mcp-tool")
    .WithHostStorage(funcStorage)
    .WithExternalHttpEndpoints()
    .WithEndpoint("http", endpoint => endpoint.Port = FunctionsMcpToolPort);

if (!isPublishMode)
{
    var showcaseAppUiBuild = builder.AddJavaScriptApp("showcase-app-ui-build", showcaseAppUiPath, "build")
        .WithNpm(installCommand: "ci", installArgs: ["--no-audit", "--no-fund"]);

    var showcaseWeatherUiBuild = builder.AddJavaScriptApp("showcase-weather-ui-build", showcaseWeatherUiPath, "build")
        .WithNpm(installCommand: "ci", installArgs: ["--no-audit", "--no-fund"]);

    var showcaseAppUiWatch = builder.AddJavaScriptApp("showcase-app-ui-watch", showcaseAppUiPath, "build:watch")
        .WithNpm(install: false)
        .WaitForCompletion(showcaseAppUiBuild);

    var showcaseWeatherUiWatch = builder.AddJavaScriptApp("showcase-weather-ui-watch", showcaseWeatherUiPath, "build:watch")
        .WithNpm(install: false)
        .WaitForCompletion(showcaseWeatherUiBuild);

    functionsMcpTool
        .WaitForCompletion(showcaseAppUiBuild)
        .WaitForCompletion(showcaseWeatherUiBuild)
        .WaitForStart(showcaseAppUiWatch)
        .WaitForStart(showcaseWeatherUiWatch);

    showcaseAppUiBuild.WithParentRelationship(functionsMcpTool);
    showcaseWeatherUiBuild.WithParentRelationship(functionsMcpTool);
    showcaseAppUiWatch.WithParentRelationship(functionsMcpTool);
    showcaseWeatherUiWatch.WithParentRelationship(functionsMcpTool);

    builder.AddFunctionsMcpInspector("mcp-inspector-showcase", functionsMcpTool,
        McpInspectorShowcaseClientPort, McpInspectorShowcaseServerPort, McpEndpointPath);
}

builder.Build().Run();
