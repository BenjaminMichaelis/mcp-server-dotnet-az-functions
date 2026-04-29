# Building Custom MCP Servers in .NET with Azure Functions

This is a quickstart template to easily build and deploy a custom remote MCP server to the cloud using Azure functions. You can clone/restore/run on your local machine with debugging, and `azd up` to have it in the cloud in a couple minutes.

The MCP server is configured with [built-in authentication](https://learn.microsoft.com/en-us/azure/app-service/overview-authentication-authorization) using Microsoft Entra as the identity provider.

You can also use [API Management](https://learn.microsoft.com/azure/api-management/secure-mcp-servers) to secure the server, as well as network isolation using VNET.

The MCP server uses [built-in authentication](https://learn.microsoft.com/en-us/azure/app-service/overview-authentication-authorization?WT.mc_id=8B97120A00B57354) via Microsoft Entra and [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview?WT.mc_id=8B97120A00B57354) for local orchestration. You can also add [API Management](https://learn.microsoft.com/azure/api-management/secure-mcp-servers?WT.mc_id=8B97120A00B57354) for extra security and VNET isolation.

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/BenjaminMichaelis/mcp-server-dotnet-az-functions)

---

## Prerequisites

### Required for all development approaches

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local?tabs=windows%2Cisolated-process%2Cnode-v4%2Cpython-v2%2Chttp-trigger%2Ccontainer-apps&pivots=programming-language-csharp&WT.mc_id=8B97120A00B57354#install-the-azure-functions-core-tools) ≥ `4.5.0` — required by both `func start` and the Aspire AppHost
- [Azure Developer CLI](https://aka.ms/azd) **1.23.x or above** (for deployment)
- An [OCI-compatible container runtime](https://aspire.dev/get-started/prerequisites/) such as [Docker Desktop](https://www.docker.com/products/docker-desktop/) or [Podman](https://podman.io/) — required for Aspire and the Azurite storage emulator

> This repo includes a `global.json` that pins to the .NET 10 SDK feature band and rolls forward within .NET 10 to the latest installed stable feature release.

### For running the Aspire AppHost (recommended)

The Aspire AppHost orchestrates all MCP servers locally. Install the [Aspire CLI](https://aspire.dev/get-started/install-cli/):

```shell
# macOS / Linux
curl -sSL https://aspire.dev/install.sh | sh

# Windows (PowerShell)
iex (iwr https://aspire.dev/install.ps1 -UseBasicParsing).Content
```

Verify the install: `aspire --version`

- [Node.js](https://nodejs.org/) 22+ (or the current LTS release) — required because the AppHost now runs the `functions-mcp-app-ui-*` and `mcp-weather-app-ui-*` JavaScript resources to build the embedded MCP App UIs

> The [Aspire VS Code extension](https://aka.ms/aspire/vscode) is an alternative to the CLI — install it and press **F5** on the AppHost project to start everything. It also installs the Aspire CLI for you via the **Aspire: Install Aspire CLI (stable)** command.

### For VS Code development (recommended editor)

- [Visual Studio Code](https://code.visualstudio.com/) 1.98+
- [Aspire extension](https://aka.ms/aspire/vscode) — run/debug the AppHost with F5; includes dashboard, resource sidebar, and MCP integration
- [Azure Functions extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions)
- [C# Dev Kit extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

### For Visual Studio development

- [Visual Studio 2025](https://visualstudio.microsoft.com/vs/) with the **Azure development** workload

### Zero-install options

Skip local setup entirely with a cloud development environment:

- **GitHub Codespaces** — Click the badge at the top of this README. The Codespace is pre-configured with .NET 10, the Aspire CLI, Docker-in-Docker, Azure Functions Core Tools, Azure CLI, azd, Node.js, MCP Inspector, and Azurite. If your Codespace was created before the Aspire AppHost was added, run **Codespaces: Rebuild Container** once to apply the updated image and features.
- **Dev Containers** — Open this repo in VS Code and select **Reopen in Container** when prompted. The Dev Container installs the same Aspire-capable toolchain as Codespaces, including the Aspire CLI and Docker-in-Docker for the AppHost and Azurite workflows. If you already opened this repo in a container befor  pulling the latest changes, rebuild it first. See [Aspire and Dev Containers](https://aspire.dev/get-started/dev-containers/) for details. 

---

## Prepare your local environment

> **Using the Aspire AppHost?** No manual setup needed — `Aspire.Hosting.Azure.Functions` automatically starts an Azurite container for every Function App when the AppHost launches. Skip this section.

If you're running a **single Function App directly** with `func start`, start Azurite manually for the snippet tools:

```shell
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 \
    mcr.microsoft.com/azure-storage/azurite
```

> Alternatively, use the [Azurite VS Code extension](https://marketplace.visualstudio.com/items?itemName=Azurite.azurite) and run **Azurite: Start** from the Command Palette.

---

## Run and Test Locally

### Recommended: All servers via Aspire + MCP Inspector

The .NET Aspire AppHost starts **all the MCP servers simultaneously** and opens a pre-configured MCP Inspector — no terminal juggling needed. This is the best way to explore every primitive in one place.

#### Option A: Aspire CLI

1. Start the AppHost from the repo root:

    ```shell
    aspire start --isolated
    ```

    The AppHost now runs first-class JavaScript resources to restore, build, and watch the Vite UIs used by `functions-mcp-app` and `mcp-weather-app` before those Function apps start.

1. Open the **Aspire dashboard** and click the **`mcp-inspector`** resource URL.

    If you edit either UI, the matching `*-ui-watch` resource rebuilds `app/dist` automatically. Rebuild the Function resource to copy the updated bundle into the Functions output:

    ```shell
    aspire resource functions-mcp-app rebuild
    aspire resource mcp-weather-app rebuild
    ```

1. When done:

    ```shell
    aspire stop
    ```

#### Option B: VS Code Aspire extension

1. Install the [Aspire VS Code extension](https://aka.ms/aspire/vscode) if you haven't already.
1. Open the repo in VS Code and open `FunctionsMcpTool.AppHost/AppHost.cs`.
1. Press **F5** (or run **Aspire: Configure launch.json file** from the Command Palette first to generate a launch config, then F5). The extension builds the AppHost, starts all services, and opens the dashboard automatically.
1. From the dashboard, click the **`mcp-inspector`** resource URL.

#### Servers pre-configured in the Inspector

The Inspector is pre-configured with all servers defined in the AppHost. Click **Connect**, then use the server dropdown to switch between servers and **List Tools**, **List Prompts**, or **List Resources**.

---

### Alternative: Single Function App with VS Code + GitHub Copilot

Use this when you want to step through the debugger or test a single project end-to-end with Copilot.

1. Start the Azurite emulator (see above), then start the Functions host:

    ```shell
    cd src/FunctionsMcpTool
    func start
    ```

    > To run Resources and Prompts alongside Tools, open extra terminals:
    > ```shell
    > cd src/FunctionsMcpResources && func start --port 7072
    > cd src/FunctionsMcpPrompts  && func start --port 7073
    > ```

1. Open **`.vscode/mcp.json`**, find the server called **`local-mcp-function`**, and click **Start** above the name. The endpoint is pre-configured:

    ```
    http://localhost:7071/runtime/webhooks/mcp
    ```

1. Open **GitHub Copilot** in **Agent** mode and try these prompts:

    ```
    Say Hello
    ```

    ```
    Save this snippet as snippet1
    ```

    ```
    Retrieve snippet1 and apply to NewFile.cs
    ```

1. When prompted to run the tool, click **Continue** to consent.

1. Press **Ctrl+C** to stop the function host when done.

---

### Verify local blob storage (optional)

After saving a snippet, confirm Azurite stored it:

**Azure Storage Explorer:** Expand **Emulator & Attached → Storage Accounts → (Emulator - Default Ports) → Blob Containers → snippets**

**Azure CLI:**

```shell
az storage blob list \
  --container-name snippets \
  --connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
```

---

## Deploy to Azure

The architecture deployed by `azd`:

![Architecture Diagram](architecture-diagram.png)

### Step 1: Sign in to Azure

```shell
az login
azd auth login
```

### Step 2: Create an environment and configure

```shell
azd env new <environment-name>
```

Pre-authorize VS Code to request access tokens from Microsoft Entra:

```shell
azd env set PRE_AUTHORIZED_CLIENT_IDS aebc6443-996d-45c2-90f0-388ff96faa56
```

**Optional:** Enable VNet isolation:

```shell
azd env set VNET_ENABLED true
```

### Step 3: Provision and deploy

1. Choose which MCP server to deploy:

    ```shell
    azd env set DEPLOY_SERVICE <tools | resources | prompts | weather | apps>
    ```

1. Provision Azure resources:

    ```shell
    azd provision
    ```

    When prompted, choose your subscription, an Azure region, and `false` for virtual network resources to keep the initial deployment simple.

1. Deploy your chosen service:

    ```shell
    azd deploy --service tools      # MCP Tools (with Entra auth)
    azd deploy --service resources  # MCP Resources
    azd deploy --service prompts    # MCP Prompts
    azd deploy --service weather    # Weather App
    azd deploy --service apps       # Fluent API App
    ```

### Step 4: Connect to the remote MCP server

1. Open **`.vscode/mcp.json`** and click **Start** above **`remote-mcp-function`**.
1. Enter your `functionapp-name` when prompted — find it in the `azd provision` output or in `.azure/<environment-name>/.env`.
1. Authenticate with Microsoft when prompted — click **Allow** and sign in with your Azure subscription account.

> [!TIP]
> A successful connection shows the number of tools the server exposes. For detailed interaction logs, click **More… → Show Output** above the server name in the MCP panel.

### Redeploy and clean up

```shell
# Redeploy everything
azd up

# Redeploy one service
azd deploy tools

# Tear down all Azure resources
azd down
```

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `Connection refused` on func start | Ensure Azurite is running: `docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite` |
| `API version not supported by Azurite` | Pull the latest image: `docker pull mcr.microsoft.com/azure-storage/azurite`, then restart Azurite and the app |
| MCP Inspector shows no servers | Make sure the function host is running first (`func start`), then click Connect in Inspector |
| VS Code MCP panel shows 0 tools | Click **More… → Show Output** on the server name for error details; check that the endpoint URL in `mcp.json` is correct |
| Aspire dashboard doesn't open | Run `aspire doctor` to check for missing prerequisites |
| Authentication fails on remote server | Confirm `PRE_AUTHORIZED_CLIENT_IDS` was set before `azd provision`; re-provision if needed |
| Can't find Function App name after deploy | Run `azd env get-values` or open `.azure/<env-name>/.env` and look for `AZURE_FUNCTION_APP_NAME` |

---

## Next Steps

- Enable **VNet isolation** for network-level security: `azd env set VNET_ENABLED true`
- Explore the [**MCP SDK for .NET**](https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html) for building pure .NET MCP servers outside of Azure Functions
- Learn more about [related MCP efforts from Microsoft](https://github.com/microsoft/mcp/tree/main/Resources)
