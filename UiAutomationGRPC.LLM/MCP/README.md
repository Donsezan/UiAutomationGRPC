# UiAutomationGRPC.LLM (MCP Server)

This is a C# MCP (Model Context Protocol) server that acts as a bridge between LLMs (like Claude/Antigravity) and the `UiAutomationGRPC.LayerServer`.

It connects to `UiAutomationGRPC.LayerServer` via gRPC (default: `http://localhost:50052`) and exposes UI automation capabilities as MCP Tools.

## Tools

### `get_app_structure`
Retrieves the full UI structure of an application as a JSON tree.
- **process_id**: Process ID of the target app.
- **app_name**: Name of the app (if Process ID is not used).
- **use_process_id**: Boolean to switch between PID and Name lookup.

### `perform_action`
Performs an action on a UI element.
- **runtime_id**: The unique ID of the element (retrieved from `get_app_structure`).
- **action**: The action to perform (e.g., `INVOKE`, `CLICK`, `SET_VALUE`, `EXPAND_COLLAPSE`).
- **arguments**: Optional list of arguments (e.g., text for `SET_VALUE`).

### `open_app`
Launches an application.
- **app_name**: Path to executable.
- **arguments**: Command line arguments.

### `close_app`
Closes an application by Process ID.
- **process_id**: The Process ID to terminate.

## Prerequisites

1. **UiAutomationGRPC.LayerServer** must be running on port `50052`.
2. .NET 8 SDK installed.

## building

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

The server currently hardcodes the gRPC address to `http://localhost:50052`. Modify `Program.cs` to change this if needed.
