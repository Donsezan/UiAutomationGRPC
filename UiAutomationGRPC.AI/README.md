# UiAutomationGRPC.AI

This directory contains tools and resources designed to enable Large Language Models (LLMs) to interact seamlessly with the **UiAutomationGRPC.Server**.

By leveraging the Server's App Structure approach, these tools provide LLMs with a structured, "semantic" view of desktop applications, facilitating a reliable **See → Think → Act** automation loop.

## Overview

The `UiAutomationGRPC.Server` provides two approaches for UI automation:
1. **Direct Element Work** - Fine-grained control for scripts
2. **App Structure (LLM-Friendly)** - JSON representation of the UI tree

The tools in this folder bridge the gap between the gRPC server and LLM interfaces.

## Folder Structure

- **[MCP](./MCP/)**: A **Model Context Protocol (MCP)** server implementation.
    - Connects to `UiAutomationGRPC.Server` via gRPC (default: `localhost:50051`)
    - Exposes callable tools to the LLM: `open_app`, `get_app_structure`, `perform_action`, `perform_action_with_structure`, `close_app`, `take_screenshot`
    
- **[Skill](./Skill/)**: Skill definitions (e.g., `SKILL.md`) that teach an LLM how to effectively use the available tools.

## How it Works

The interaction follows a standard loop:
1.  **See**: The LLM calls `get_app_structure` to retrieve a JSON map of the current application window.
2.  **Think**: The LLM parses the JSON, identifying the necessary UI elements (Buttons, TextBoxes) and their IDs.
3.  **Act**: The LLM calls `perform_action_with_structure` to interact with the chosen element and receive updated UI state.

## Example: MCP Tool Usage

```
1.  User: "Open Calculator and calculate 5 + 5."
2.  LLM (Tool Call): open_app(app_name="calc")
3.  LLM (Tool Call): get_app_structure(app_name="calc")
    → Server returns JSON describing the Calculator UI.
4.  LLM (Reasoning): "I see buttons for Five, Plus, and Equals."
5.  LLM (Tool Call): perform_action_with_structure(runtime_id="...", action="INVOKE")
    → Clicks Five and returns updated structure
6.  Continue clicking Plus, Five, Equals...
```

## Security

Security is configured on the **UiAutomationGRPC.Server** side (see [Server README](../UiAutomationGRPC.Server/README.md#security)). The MCP server connects to the gRPC server and must match its security mode:

| Server Mode | MCP Configuration |
|-------------|-------------------|
| Insecure (HTTP) | `UIAUTOMATION_INSECURE_MODE=true` |
| HTTPS | Default (uses HTTPS) |
| HTTPS + Token | Set `UIAUTOMATION_AUTH_TOKEN` |

> See [MCP README](./MCP/README.md#security-modes) for environment variable details.

## Related Documentation

- [Server README](../UiAutomationGRPC.Server/README.md) - Server API, security, and configuration
- [MCP Server README](./MCP/README.md) - Detailed MCP tool documentation
- [Skill Instructions](./Skill/UiAutomationSkill/SKILL.md) - Full LLM skill definition
