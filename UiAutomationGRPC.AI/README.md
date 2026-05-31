# UiAutomationGRPC.AI

Let an LLM control any Windows desktop app — click buttons, read values, fill forms, navigate menus — by connecting it to `UiAutomationGRPC.Server` over MCP.

---

## How it works

There are two pieces:

| Piece | What it is |
|-------|-----------|
| **MCP server** | A C# process that exposes UI automation as callable tools the LLM can invoke |
| **Skill** | A markdown guide that teaches the LLM *how* to use those tools effectively |

The LLM follows a **See → Think → Act** loop:

```
SEE   →  get_app_structure       returns the app's UI tree as JSON
THINK →  find the target element by AutomationId / Name / RuntimeId
ACT   →  perform_action_with_structure  executes the action and returns the refreshed tree
REPEAT → updated tree comes back; continue from new state
```

Each action returns the refreshed UI tree in the same call, so the LLM always has an up-to-date view without extra round-trips.

---

## MCP Server

The MCP server (`UiAutomationGRPC.AI/MCP/`) connects to `UiAutomationGRPC.Server` over gRPC and exposes its capabilities as [Model Context Protocol](https://modelcontextprotocol.io) tools — the standard way LLMs call external services.

### Available tools

| Tool | Purpose |
|------|---------|
| `open_app` | Launch an app by name or full path |
| `close_app` | Terminate an app by process ID |
| `get_app_structure` | Get the full UI tree as JSON (the "See" step) |
| `find_element` | Find one element by `AutomationId`, `Name`, `ClassName`, or `ControlType` |
| `get_children` | List direct children of an element |
| `get_property` | Read one property: `Name`, `IsEnabled`, `Value`, etc. |
| `perform_action` | Execute an action on an element |
| `perform_action_with_structure` | Execute action **and** return the refreshed UI — preferred for LLM loops |
| `send_keys` | Send keyboard input (`{ENTER}`, `^a`, `^s`, etc.) |
| `take_screenshot` | Capture a window or element as a PNG the LLM can see |
| `clear_cache` | Flush the server-side element cache |

### Setup

**Step 1 — Start the gRPC server**

The MCP server is a bridge; the actual automation runs in `UiAutomationGRPC.Server`. Start it first (run as Administrator — UIA requires elevation):

```powershell
dotnet run --project UiAutomationGRPC.Server
```

**Step 2 — Build the MCP server**

```powershell
dotnet build UiAutomationGRPC.AI/MCP/UiAutomationGRPC.LLM.csproj
```

> Build once and point your LLM client at the compiled `.exe`. Using `dotnet run` directly can cause startup timeouts if NuGet package sources are slow.

**Step 3 — Connect your LLM client**

Add the MCP server to your client's config. Example for **Claude Code** (`.mcp.json` at workspace root):

```json
{
  "mcpServers": {
    "uiautomation": {
      "command": "<absolute_path>/UiAutomationGRPC.AI/MCP/bin/Debug/net8.0/UiAutomationGRPC.LLM.exe",
      "env": {
        "UIAUTOMATION_SERVER_ADDRESS": "http://localhost:50051",
        "UIAUTOMATION_INSECURE_MODE": "true"
      }
    }
  }
}
```

For **Claude Desktop, Cursor, Windsurf** — use `dotnet run --no-build` or the same `.exe` path in your client's MCP config block.

### Configuration

| Environment variable | Description | Default |
|----------------------|-------------|---------|
| `UIAUTOMATION_SERVER_ADDRESS` | gRPC server address | `http://localhost:50051` |
| `UIAUTOMATION_INSECURE_MODE` | Set `true` for plain HTTP (development only) | `false` |
| `UIAUTOMATION_AUTH_TOKEN` | Bearer token (when server has token auth enabled) | *(none)* |

---

## Skill

The Skill (`UiAutomationGRPC.AI/Skill/UiAutomationSkill/SKILL.md`) is a plain-text guide loaded into the LLM's context. It tells the LLM:

- Which tool to use for which situation
- How to handle edge cases (UWP apps, custom-drawn controls, popups)
- What to do when an action fails

The LLM follows the Skill automatically — no code changes needed. To improve how the LLM behaves, edit `SKILL.md`.

### Mental maps

The `apps/` folder contains per-app markdown files that document stable element IDs for specific applications:

```
apps/
  calculator.md   — AutomationIds for all buttons, display, mode switcher
  notepad.md      — text area ID, save dialog quirks, key patterns
  _template.md    — blank template for new apps
```

**Why mental maps matter:** Windows UI Automation element IDs (`AutomationId`) are stable across sessions for well-built apps. A mental map lets the LLM skip the full tree scan and go directly to the element it needs — faster, more reliable, fewer API calls.

**When a mental map exists**, the LLM reads it at the start and navigates directly:
```
find_element(AutomationId="CalculatorResults")  →  click num5Button  →  click multiplyButton  →  ...
```

**When no map exists**, the LLM calls `get_app_structure` to discover the UI, then creates a map so the next session is faster. Maps are created automatically after the first exploration of an unknown app.

#### Adding a map for a new app

1. Copy `apps/_template.md` to `apps/{appname}.md`.
2. Fill in the stable `AutomationId`s and any quirks you discover.
3. The LLM will pick it up automatically on the next session.

---

## Example session

```
User: "Open Calculator and compute 9 × 9"

LLM: open_app("calc")
LLM: find_element(AutomationId="CalculatorResults")   ← ready-probe from mental map
LLM: find_element(AutomationId="num9Button")
LLM: perform_action_with_structure(num9Button, INVOKE) → tree updated, Display is 9
LLM: perform_action_with_structure(multiplyButton, INVOKE)
LLM: perform_action_with_structure(num9Button, INVOKE)
LLM: perform_action_with_structure(equalButton, INVOKE) → Display is 81
LLM: get_property(CalculatorResults, "Name")           → "Display is 81"

Result: 81 ✓
```

---

## Security note

The gRPC server can launch processes, synthesize keyboard/mouse input, and terminate apps. By default it binds to `127.0.0.1` only. Enable `Security:AllowRemote` only if you understand the implications. See [UiAutomationGRPC.Server README](../UiAutomationGRPC.Server/README.md) for full security configuration.
