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

// ─── Storage ───────────────────────────────────────────────────────
var funcStorage = builder.AddAzureStorage("func-storage").RunAsEmulator();

// ─── mcp-run-timer + its inspector ────────────────────────────────
var mcpRunTimer = builder.AddAzureFunctionsProject<Projects.McpRunTimer>("mcp-run-timer")
    .WithHostStorage(funcStorage)
    .WithExternalHttpEndpoints()
    .WaitForCompletion(timerAppUiBuild)
    .WaitForStart(timerAppUiWatch);

timerAppUiBuild.WithParentRelationship(mcpRunTimer);
timerAppUiWatch.WithParentRelationship(mcpRunTimer);

builder.AddFunctionsMcpInspector("mcp-inspector-timer", mcpRunTimer,
    McpInspectorTimerClientPort, McpInspectorTimerServerPort, McpEndpointPath);

// ─── functions-mcp-tool + its inspector ───────────────────────────
var functionsMcpTool = builder.AddAzureFunctionsProject<Projects.FunctionsMcpTool>("functions-mcp-tool")
    .WithHostStorage(funcStorage)
    .WithExternalHttpEndpoints()
    .WaitForCompletion(showcaseAppUiBuild)
    .WaitForStart(showcaseAppUiWatch)
    .WaitForCompletion(showcaseWeatherUiBuild)
    .WaitForStart(showcaseWeatherUiWatch);

showcaseAppUiBuild.WithParentRelationship(functionsMcpTool);
showcaseAppUiWatch.WithParentRelationship(functionsMcpTool);
showcaseWeatherUiBuild.WithParentRelationship(functionsMcpTool);
showcaseWeatherUiWatch.WithParentRelationship(functionsMcpTool);

builder.AddFunctionsMcpInspector("mcp-inspector-showcase", functionsMcpTool,
    McpInspectorShowcaseClientPort, McpInspectorShowcaseServerPort, McpEndpointPath);

builder.Build().Run();
