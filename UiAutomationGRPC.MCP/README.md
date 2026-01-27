# UiAutomationGRPC MCP Server

This is an MCP (Model Context Protocol) server that allows Claude to interact with Windows UI Automation via the `UiAutomationGRPC` service.

## Prerequisites

1.  **UiAutomationGRPC Server**: You must have the main gRPC server running.
    -   Build the solution.
    -   Run `UiAutomationGRPC.Server.exe` (usually in `UiAutomationGRPC.Server/bin/Debug`).
    -   It listens on port `50051`.

2.  **.NET 6.0 SDK**: Required to build/run the MCP server.

## Installation

1.  Build the MCP server:
    ```powershell
    dotnet build
    ```

## Usage with Claude Desktop / Claude Code

To use this with Claude, you need to configure it as an MCP server in your Claude config file (usually `%APPDATA%\Claude\claude_desktop_config.json` or similar for other clients).

**Config Example:**

```json
{
  "mcpServers": {
    "uiautomation": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\WorkPlace\\c#\\UiAutomationGRPC\\UiAutomationGRPC\\UiAutomationGRPC.MCP\\UiAutomationGRPC.MCP.csproj"
      ]
    }
  }
}
```

*Note: Replace the path with the actual absolute path to your `.csproj` file.*

Alternatively, you can point to the compiled executable:

```json
{
  "mcpServers": {
    "uiautomation": {
      "command": "D:\\WorkPlace\\c#\\UiAutomationGRPC\\UiAutomationGRPC\\UiAutomationGRPC.MCP\\bin\\Debug\\net6.0\\UiAutomationGRPC.MCP.exe",
      "args": []
    }
  }
}
```

## Available Tools

-   **`open_app`**: Launches an application (e.g., `calc.exe`).
-   **`find_element`**: Finds UI elements.
-   **`perform_action`**: Clicks, toggles, invokes elements.
-   **`get_property`**: Reads values (e.g., Calculator result).
-   **`take_screenshot`**: Captures window or element screenshots.

## Example Scenario: Calculator

1.  **Open Calculator**:
    ```text
    Call open_app with app_name="calc.exe"
    ```
2.  **Find Buttons**:
    ```text
    Call find_element with condition_type="property", property_name="Name", property_value="Two"
    ```
3.  **Click**:
    ```text
    Call perform_action with runtime_id="...", action="CLICK"
    ```
