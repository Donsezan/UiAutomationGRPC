---
name: Windows UI Automation Control
description: Control Windows applications using the UiAutomationGRPC LayerServer and gRPC calls. This skill enables a "See -> Think -> Act" loop for interacting with desktop UIs.
---

# Windows UI Automation Control

This skill allows you to control Windows applications by interacting with the `UiAutomationGRPC.LayerServer`. It abstracts the complexity of UI automation into a simple JSON-based structure and a set of actions.

## 1. Prerequisites

- **LayerServer Running**: Ensure `UiAutomationGRPC.LayerServer.exe` is running on `localhost:50052`.
- **grpccurl**: This tool is used to send gRPC requests.

## 2. The Loop: See -> Think -> Act

The core interaction model is a loop:
1.  **See**: Get the current state of the application (JSON tree).
2.  **Think**: Analyze the JSON to find the element you want to interact with (find its `UniqId`).
3.  **Act**: Send an action (Click, Type, etc.) to that element using its `UniqId`.
4.  **Repeat**: The action response includes the new state, so you can immediately plan the next move.

## 3. Commands

### Step 1: Get App Structure (See)

Retrieve the full UI tree of an application.

```bash
grpccurl -plaintext -d '{"app_name": "calc"}' localhost:50052 UiAutomation.UiAutomationService/GetAppStructure
```

- **app_name**: The name of the process (e.g., "calc", "notepad").
- **Output**: Returns `json_structure` containing the tree of `AppNode`s.

### Understanding the AppNode

The JSON structure consists of nested nodes. Key fields:

```json
{
  "UniqId": "42,12345...",        // <--- CRITICAL: Use this ID for actions
  "Name": "Five",                 // Visual text or name
  "AutomationId": "num5Button",   // Stable ID (useful for confirmation)
  "ControlType": "Button",        // Type of element
  "BoundingRectangle": "...",     // coordinates
  "Children": [ ... ]
}
```

### Step 2: Perform Action (Act)

Perform an action on an element and get the updated structure back immediately.

```bash
grpccurl -plaintext -d '{"runtime_id": "YOUR_UNIQ_ID_HERE", "action": 9}' localhost:50052 UiAutomation.UiAutomationService/PerformActionWithStructure
```

- **runtime_id**: The `UniqId` found in the JSON node.
- **action**: The integer code for the action (see reference below).
- **arguments**: Optional list of strings.

### Action Reference

| Action Name | Code | Description | Arguments |
| :--- | :--- | :--- | :--- |
| **INVOKE** | 0 | Trigger default action (e.g., press button) | None |
| **SET_VALUE** | 4 | Type text into a field | `["Text to type"]` |
| **SET_FOCUS** | 5 | Focus the element | None |
| **MoveTo** | 8 | Move mouse cursor to element center | None |
| **LeftClick** | 9 | Simulate Left Click (Recommended) | None |
| **RightClick** | 10 | Simulate Right Click | None |
| **DoubleClick** | 17 | Simulate Double Click | None |

### Examples

#### Click the "5" button
1. Find node with `Name: "Five"` or `AutomationId: "num5Button"`.
2. Extract `UniqId` (e.g., "42,333").
3. Call:
```bash
grpccurl -plaintext -d '{"runtime_id": "42,333", "action": 9}' localhost:50052 UiAutomation.UiAutomationService/PerformActionWithStructure
```

#### Type "Hello" into Notepad
1. Find node with `ControlType: "Document"` or `Name: "Text Editor"`.
2. Extract `UniqId` (e.g., "42,999").
3. Call:
```bash
grpccurl -plaintext -d '{"runtime_id": "42,999", "action": 4, "arguments": ["Hello"]}' localhost:50052 UiAutomation.UiAutomationService/PerformActionWithStructure
```

## 4. Troubleshooting

- **App Not Found**: Ensure the app is running. If not, you can try starting it via command line or ensure `GetAppStructure` is called with arguments if supported by your setup.
- **Element Not Found**: `UniqId`s are runtime-specific. If the app restarts, IDs change. Always fetch a fresh structure.
- **Access Denied**: Some apps require Admin privileges.
