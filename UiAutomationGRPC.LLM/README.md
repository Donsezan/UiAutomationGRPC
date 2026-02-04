# UiAutomationGRPC.LLM

This directory contains tools and resources designed to enable Large Language Models (LLMs) to interact seamlessly with the **UiAutomationGRPC.LayerServer**.

By leveraging the `LayerServer`, these tools provide LLMs with a structured, "semantic" view of desktop applications, facilitating a reliable **See -> Think -> Act** automation loop.

## Overview

The `UiAutomationGRPC.LayerServer` abstracts the complexity of raw UI Automation, providing a simplified JSON representation of the application's UI tree. The tools in this folder bridge the gap between that gRPC server and LLM interfaces.

## Folder Structure

- **[MCP](./MCP/)**: Contains a **Model Context Protocol (MCP)** server implementation.
    - This server connects to `UiAutomationGRPC.LayerServer` via gRPC.
    - It exposes callable tools to the LLM (e.g., `get_app_structure`, `perform_action`).
    
- **[Skill](./Skill/)**: Contains specific skill definitions or prompts (e.g., `SKILL.md`) that teach an LLM how to effectively use the available tools to navigate and control applications.

## How it Works

The interaction follows a standard loop:
1.  **See**: The LLM calls a tool (e.g., `GetAppStructure`) to retrieve a JSON map of the current application window.
2.  **Think**: The LLM parses the JSON, identifying the necessary UI elements (Buttons, TextBoxes) and their IDs or coordinates.
3.  **Act**: The LLM calls an action tool (e.g., `PerformAction`) to click, type, or interact with the chosen key.

## Examples

### Example 1: MCP Tool Usage
If you are using the MCP server, the LLM might execute a flow like this:

1.  **User**: "Open Calculator and calculate 5 + 5."
2.  **LLM (Tool Call)**: `OpenApp("calc")`
3.  **LLM (Tool Call)**: `GetAppStructure()`
    *   *Server returns JSON describing the Calculator UI.*
4.  **LLM (Reasoning)**: "I see a button with valid 'Five', 'Plus', and 'Equals'."
5.  **LLM (Tool Call)**: `PerformAction("Five", "Click")`
6.  **LLM (Tool Call)**: `PerformAction("Plus", "Click")`
7.  **LLM (Tool Call)**: `PerformAction("Five", "Click")`
8.  **LLM (Tool Call)**: `PerformAction("Equals", "Click")`

### Example 2: Using the Skill
Refer to [Skill/UiAutomationSkill/SKILL.md](./Skill/UiAutomationSkill/SKILL.md) for detailed instructions on how to load the skill into your LLM context.
