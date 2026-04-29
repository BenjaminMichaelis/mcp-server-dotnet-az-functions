using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static FunctionsMcpTool.ToolsInformation;
using static FunctionsMcpTool.ResourcesInformation;
using static FunctionsMcpTool.PromptsInformation;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddFunctionsServiceDefaults();

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddSingleton(_ => new BlobServiceClient(
        Environment.GetEnvironmentVariable("AzureWebJobsStorage")));

// ─── Tools ─────────────────────────────────────────────────────────
// Demonstrate how you can define tool properties in Program.cs
// without requiring McpToolProperty input binding attributes:
builder
    .ConfigureMcpTool(EchoToolName)
    .WithProperty(EchoMessagePropertyName, McpToolPropertyType.String, EchoMessagePropertyDescription, required: true);

// ─── Resources ─────────────────────────────────────────────────────
// Configure metadata on resources:
builder
    .ConfigureMcpResource(ServerInfoResourceUri)
    .WithMetadata("cache", new { ttlSeconds = 60 });

// ─── Prompts ───────────────────────────────────────────────────────
// Configure prompt arguments in Program.cs:
builder
    .ConfigureMcpPrompt(GenerateDocsPromptName)
    .WithArgument(GenerateDocsFunctionNameArgName, GenerateDocsFunctionNameArgDescription, required: true)
    .WithArgument(GenerateDocsStyleArgName, GenerateDocsStyleArgDescription);

// ─── MCP Apps ──────────────────────────────────────────────────────
// Simple app with a file-backed view
builder.ConfigureMcpTool("HelloApp")
    .AsMcpApp(app => app
        .WithView("assets/hello-app.html")
        .WithTitle("Hello App")
        .WithBorder());

// Dynamic dashboard built with Vite + @modelcontextprotocol/ext-apps SDK
builder.ConfigureMcpTool("SnippetDashboard")
    .AsMcpApp(app => app
        .WithView("app/dist/index.html")
        .WithTitle("Snippet Dashboard")
        .WithPermissions(McpAppPermissions.ClipboardWrite | McpAppPermissions.ClipboardRead)
        .WithCsp(csp =>
        {
            csp.ConnectTo("https://api.example.com")
               .LoadResourcesFrom("https://cdn.example.com");
        })
        .ConfigureApp()
        .WithStaticAssets("app/dist")
        .WithVisibility(McpVisibility.Model | McpVisibility.App));

builder.Build().Run();
