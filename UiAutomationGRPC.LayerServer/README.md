# UiAutomationGRPC.LayerServer

A gRPC-based server that provides high-level access to Windows UI Automation. It is designed to act as an intelligent layer between raw automation APIs and consumers like Large Language Models (LLMs) or automation scripts.

## Overview

The LayerServer abstracts the complexity of `System.Windows.Automation` into a simple interaction model:
1. **Explore**: Retrieve the full structure of an application window as a JSON tree.
2. **Act**: Perform actions on specific elements (Click, Type, etc.) using unique IDs from the tree.
3. **Observe**: Receive the updated structure immediately after an action to decide the next step.

## Key Features

- **Stateful Interaction**: Maintains a cache of runtime elements, allowing efficient re-access.
- **JSON Structure**: Converts complex UI trees into clean, LLM-friendly JSON.
- **Rich Actions**: Supports Invoke, Click (Left/Right/Double), MoveTo, SetValue, and more.
- **Process Management**: Can launch and close applications.

## API Methods

### `GetAppStructure`
Retrieves the UI structure of a running application.

**Request:**
```protobuf
message AppStructureRequest {
    string app_name = 1;     // Name of the process (e.g., "calc") or Window Name
    int32 process_id = 2;    // Optional: Target specific PID
    bool use_process_id = 3; // Toggle to use PID instead of name
    string arguments = 4;    // Optional: Args to start app if not running
}
```

**Response:**
```protobuf
message AppStructureResponse {
    string json_structure = 1; // The UI Tree in JSON
    bool success = 2;
    string message = 3;
}
```

### `PerformActionWithStructure`
Performs an action on a specific element and returns the *updated* application structure in one go. This is ideal for "Agentic" loops.

**Request:**
```protobuf
message PerformActionRequest {
    string runtime_id = 1;      // "UniqId" from the JSON node
    ActionType action = 2;      // Action to perform
    repeated string arguments = 3; // Arguments (e.g., text for SetValue)
}
```

**Supported Actions:**
- `INVOKE`, `TOGGLE`, `SELECT`, `SET_VALUE`, `SET_FOCUS`
- `CLICK` (Smart Click), `LeftClick`, `RightClick`, `DoubleClick`
- `MoveTo` (Hover)

### `CloseApp`
Closes the application by name or window.

## JSON Structure Object

The core data object returned is the `AppNode`. It represents a UI element.

```json
{
  "UniqId": "42,12345,6,789...",   // <--- Use this for "runtime_id" in Actions
  "UiAutomationId": "num5Button",
  "Name": "Five",
  "ControlType": "Button",
  "IsClickable": true,
  "IsVisible": true,
  "BoundingRectangle": "100,200,50,30", // Left,Top,Width,Height
  "Children": [ ... ]
}
```

## Integration with LLMs (e.g., Claude)

This server is designed to enable LLMs to control applications via a "See -> Think -> Act" loop.

### How it works via Tool Use / gRPC Client

1. **Launch/Connect**: The LLM calls `GetAppStructure(app_name="notepad")`.
    - *Server launches Notepad and returns the JSON tree.*
2. **Analyze**: The LLM reads the JSON.
    - *Thought: "I need to type 'Hello' into the document. I see an element with `ControlType: Document` and Name 'Text Editor' with `UniqId: 42,666`."*
3. **Act**: The LLM calls `PerformActionWithStructure(runtime_id="42,666", action=SET_VALUE, arguments=["Hello"])`.
    - *Server focuses the element, types "Hello", and returns the new JSON tree.*
4. **Verify**: The LLM checks the returned JSON.
    - *Thought: "The document now contains 'Hello'. Task complete."*

### Example: `curlGRPC` Tool Usage

If you have a tool that can make gRPC calls (like `grpccurl`), you can verify operation manually:

**List Methods:**
```bash
grpccurl -plaintext localhost:50052 list UiAutomation.UiAutomationService
```

**Get Structure:**
```bash
grpccurl -plaintext -d '{"app_name": "calc"}' localhost:50052 UiAutomation.UiAutomationService/GetAppStructure
```

**Click Button (using ID found in previous step):**
```bash
grpccurl -plaintext -d '{"runtime_id": "42,123...", "action": 9}' localhost:50052 UiAutomation.UiAutomationService/PerformActionWithStructure
```
*(Action 9 is LeftClick)*

## Building and Running

1. **Build**: `dotnet build`
2. **Run**: `dotnet run` (Listens on port 50052 by default)
3. **Test**: Use the `Test/test_layerserver.py` script to verify functionality.
