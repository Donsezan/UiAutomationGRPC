# UiAutomationGRPC.LLM (MCP Server)

A C# MCP (Model Context Protocol) server that bridges LLMs (like Claude/Antigravity) with `UiAutomationGRPC.Server` for UI automation.

Connects via gRPC (default: `http://localhost:50051`) and exposes UI automation capabilities as MCP Tools.

## Tools

### `open_app`
Launches an application.
- **app_name**: Path to executable.
- **arguments**: Command line arguments.

### `get_app_structure`
Retrieves the full UI structure of an application as a JSON tree.
- **process_id**: Process ID of the target app.
- **app_name**: Name of the app (if Process ID is not used).
- **use_process_id**: Boolean to switch between PID and Name lookup.

### `perform_action`
Performs an action on a UI element.
- **runtime_id**: The unique ID of the element (from `get_app_structure`).
- **action**: The action to perform (e.g., `INVOKE`, `CLICK`, `SET_VALUE`, `EXPAND_COLLAPSE`).
- **arguments**: Optional list of arguments (e.g., text for `SET_VALUE`).

### `perform_action_with_structure`
Performs an action on a UI element and returns the updated app structure. Ideal for LLM "See → Think → Act" loops.
- **runtime_id**: The unique ID of the element.
- **action**: The action to perform.
- **arguments**: Optional list of arguments.

### `close_app`
Closes an application by Process ID.
- **process_id**: The Process ID to terminate.

### `take_screenshot`
Takes a screenshot of the application window or a specific element. Saves the image to a temp folder and returns the file path (so the LLM can access the image file).
- **mode**: `element` or `window`.
- **runtime_id**: Required for `element` mode, optional for `window` mode.
- **process_id**: Optional, used for `window` mode if `runtime_id` is not provided.

## Prerequisites

1. **UiAutomationGRPC.Server** must be running on port `50051`.
2. .NET 8 SDK installed.

## Building

```powershell
dotnet build
```

## Running

```powershell
dotnet run
```

Or configure your MCP client (like Claude Desktop) to run:
`dotnet run --project <path_to_csproj>`

## Configuration

The server currently hardcodes the gRPC address to `http://localhost:50051`. Modify `Program.cs` to change this if needed.
