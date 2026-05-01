using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddFunctionsServiceDefaults();

builder.ConfigureFunctionsWebApplication();

// ─── MCP App: get_elapsed renders an interactive timer widget ───────
builder.ConfigureMcpTool("get_elapsed")
    .AsMcpApp(app => app
        .WithView("app/dist/index.html")
        .WithTitle("Run Timer"));

builder.Build().Run();
