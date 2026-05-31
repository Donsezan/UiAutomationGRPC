# UiAutomationGRPC.AI.MCP

A C# MCP (Model Context Protocol) server that bridges LLMs (like Claude/Antigravity) with `UiAutomationGRPC.Server` for Windows UI automation.

Built on the official **[ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) C# SDK** (stdio transport, proper protocol lifecycle/version negotiation, cancellation). Connects to the gRPC server (default: `https://localhost:50051`) and exposes UI automation capabilities as MCP Tools — the same capabilities the `UiAutomationGRPC.Library` client SDK offers for QA scripting, so an LLM can drive the same See → Think → Act and element-level flows.

> **Tool argument names are camelCase** (e.g. `appName`, `processId`, `runtimeId`, `useProcessId`) — they are derived from the SDK tool signatures. The tool *names* remain snake_case (`open_app`, `get_app_structure`, …).

## Tools

### `open_app`
Launches an application.
- **appName**: Path to executable or app name (e.g., `calc`, `notepad`).
- **arguments**: Optional command line arguments.

> For UWP/Store apps (e.g. `calc`) the returned PID may be a launcher/host process — prefer addressing such apps by name in `get_app_structure`.

### `get_app_structure`
Retrieves the full UI structure of an application as a compact JSON tree.
- **useProcessId**: Boolean to switch between PID and Name lookup.
- **processId**: Process ID of the target app (when `useProcessId` is true).
- **appName**: Name of the app (when `useProcessId` is false).

### `perform_action`
Performs an action on a UI element.
- **runtimeId**: The unique ID of the element (from `get_app_structure` / `find_element`).
- **action**: The action to perform (e.g., `INVOKE`, `LeftClick`, `SET_VALUE`, `EXPAND_COLLAPSE`).
- **arguments**: Optional list of arguments (e.g., text for `SET_VALUE`).

### `perform_action_with_structure`
Performs an action on a UI element and returns the updated app structure. Ideal for LLM "See → Think → Act" loops.
- **runtimeId**: The unique ID of the element.
- **action**: The action to perform.
- **arguments**: Optional list of arguments.

### `close_app`
Closes an application by Process ID.
- **processId**: The Process ID to terminate.

### `find_element`
Finds a single element by a property condition and returns its `runtime_id`.
- **propertyName** / **propertyValue**: e.g. `Name` / `Save`, `AutomationId` / `num2Button`, `ControlType` / `Button`.
- **startRuntimeId**: Optional. Search under this element; empty searches from the desktop root.
- **scope**: Optional. `ELEMENT`, `CHILDREN`, `DESCENDANTS` (default), `SUBTREE`, `PARENT`, `ANCESTORS`.
- **propertyType**: Optional value-type hint — `STRING` (default), `BOOL`, `INT`.

### `get_children`
Returns the immediate child elements of an element (or the desktop when `runtimeId` is empty), each with its `runtime_id` and identifying properties.
- **runtimeId**: Optional. Parent element; empty for the desktop root.

### `get_property`
Reads a single UI Automation property of an element.
- **runtimeId**: The element to read.
- **propertyName**: e.g. `Name`, `IsEnabled`, `Value`.

### `send_keys`
Sends keystrokes (`System.Windows.Forms.SendKeys` syntax, e.g. `{ENTER}`, `^a`).
- **keys**: The keys to send.
- **runtimeId**: Optional. When set, the element is focused first so keys land on that control; otherwise keys go to whatever currently has focus.
- **wait**: Optional. Wait for the keys to be processed (default `true`).

### `take_screenshot`
Takes a screenshot of the application window or a specific element and returns it **as MCP image content (base64 PNG)** the model can see directly — no temp file.
- **mode**: `element` or `window`.
- **runtimeId**: Required for `element` mode, optional for `window` mode.
- **processId**: Optional, used for `window` mode if `runtimeId` is not provided.

### `clear_cache`
Clears the server-side element cache. Call without arguments to clear all, or scope to a specific application.
- **processId**: Optional. Clear cache for a specific process ID.
- **appName**: Optional. Clear cache by application name (like `close_app`).

## Prerequisites

1. **UiAutomationGRPC.Server** must be running on port `50051`.
2. .NET 8 SDK installed.

## Building

```powershell
dotnet build UiAutomationGRPC.AI/MCP/UiAutomationGRPC.LLM.csproj
```

The compiled binary lands at `UiAutomationGRPC.AI/MCP/bin/Debug/net8.0/UiAutomationGRPC.LLM.exe`.

## Running manually

```powershell
dotnet run --project UiAutomationGRPC.AI/MCP
```

## Connecting to Claude Code (VS Code extension or CLI)

Claude Code reads project MCP servers from a `.mcp.json` file at the **workspace root** — not from `~/.claude/settings.json` (which does not accept `mcpServers`).

### Step 1 — Build the binary

```powershell
dotnet build UiAutomationGRPC.AI/MCP/UiAutomationGRPC.LLM.csproj
```

> **Why use the pre-built `.exe` instead of `dotnet run`?**  
> `dotnet run` triggers a NuGet package restore on every startup. If your global `NuGet.Config` contains any unreachable package source (e.g., a stale EAP feed), the restore times out after ~5 s — exceeding Claude Code's MCP handshake deadline before the server even starts. Pointing directly to the `.exe` skips restore entirely and starts the server in under 2 seconds.

### Step 2 — Create `.mcp.json` at the workspace root

A pre-created `.mcp.json` is already included in this repository. Its content:

```json
{
  "mcpServers": {
    "uiautomation": {
      "command": "d:\\WorkPlace\\c#\\UiAutomationGRPC\\UiAutomationGRPC\\UiAutomationGRPC.AI\\MCP\\bin\\Debug\\net8.0\\UiAutomationGRPC.LLM.exe",
      "env": {
        "UIAUTOMATION_SERVER_ADDRESS": "http://localhost:50051",
        "UIAUTOMATION_INSECURE_MODE": "true"
      }
    }
  }
}
```

Update the `command` path to match your own checkout location.

### Step 3 — Approve the server in Claude Code

1. Reload VS Code (or run **Developer: Reload Window**).
2. Claude Code detects `.mcp.json` and shows a **trust prompt** — click **Allow**.
3. The `uiautomation` server appears as connected. Verify with `/mcp` in the chat.

> **Note:** After rebuilding the project, no config change is needed — Claude Code always invokes the same binary path.

### Connecting to other MCP clients (Claude Desktop, Cursor, Windsurf)

Configure your client to run either the pre-built binary or `dotnet run`:

```json
{
  "mcpServers": {
    "uiautomation": {
      "command": "dotnet",
      "args": [
        "run",
        "--no-build",
        "--project",
        "<absolute_path_to>/UiAutomationGRPC.AI/MCP/UiAutomationGRPC.LLM.csproj"
      ],
      "env": {
        "UIAUTOMATION_SERVER_ADDRESS": "http://localhost:50051",
        "UIAUTOMATION_INSECURE_MODE": "true"
      }
    }
  }
}
```

Use `--no-build` to avoid NuGet restore delays (build separately first with `dotnet build`).

## Configuration

Configure the MCP server using environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `UIAUTOMATION_SERVER_ADDRESS` | gRPC server address | `http://localhost:50051` |
| `UIAUTOMATION_AUTH_TOKEN` | Bearer token for authentication (if server has token auth enabled) | *(none)* |
| `UIAUTOMATION_INSECURE_MODE` | Set to `true` to use HTTP instead of HTTPS | `false` |

### Security Modes

**Secure Mode (Default)**
```powershell
$env:UIAUTOMATION_SERVER_ADDRESS = "https://localhost:50051"
$env:UIAUTOMATION_AUTH_TOKEN = "your-secret-token"
dotnet run
```

**Insecure Mode (Development Only)**
```powershell
$env:UIAUTOMATION_SERVER_ADDRESS = "http://localhost:50051"
$env:UIAUTOMATION_INSECURE_MODE = "true"
dotnet run
```

> ⚠️ **Warning**: Insecure mode should only be used for development/testing. Production deployments should use HTTPS with token authentication.
