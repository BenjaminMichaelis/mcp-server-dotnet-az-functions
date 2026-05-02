using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddFunctionsServiceDefaults();

builder.ConfigureFunctionsWebApplication();

// ─── MCP App: open_timer is the interactive timer widget entry point.
// The UI detects model-initiated start_timer/stop_timer calls via session polling.
builder.ConfigureMcpTool("open_timer")
    .AsMcpApp(app => app
        .WithView("app/dist/index.html")
        .WithTitle("Timer"));

builder.Build().Run();
