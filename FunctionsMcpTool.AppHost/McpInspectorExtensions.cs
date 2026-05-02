internal static class McpInspectorExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds an MCP Inspector wired to a single Azure Functions MCP server,
        /// pre-configured with the <c>MCP_PROXY_PORT</c> query parameter on the client URL.
        /// </summary>
        internal IResourceBuilder<McpInspectorResource> AddFunctionsMcpInspector<TResource>(
            string name,
            IResourceBuilder<TResource> mcpServer,
            int clientPort,
            int serverPort,
            string path = "/runtime/webhooks/mcp",
            string inspectorVersion = "0.21.2")
            where TResource : IResourceWithEndpoints
        {
            return builder
                .AddMcpInspector(name, options =>
                {
                    options.ClientPort = clientPort;
                    options.ServerPort = serverPort;
                    options.InspectorVersion = inspectorVersion;
                })
                .WithMcpServer(mcpServer, isDefault: true, path: path)
                .WithUrls(context =>
                {
                    foreach (var url in context.Urls)
                    {
                        var isClientUrl = string.Equals(url.DisplayText, "Client", StringComparison.Ordinal)
                            || url.Url.Contains($":{clientPort}/", StringComparison.Ordinal);

                        if (!isClientUrl || url.Url.Contains("MCP_PROXY_PORT=", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var separator = url.Url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
                        url.Url = $"{url.Url}{separator}MCP_PROXY_PORT={serverPort}";
                    }

                    return Task.CompletedTask;
                });
        }
    }
}
