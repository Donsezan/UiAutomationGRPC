---
name: Windows UI Automation Control
description: Control Windows applications using UiAutomationGRPC.Server with the "See → Think → Act" loop for efficient LLM-driven UI automation.
---

# Windows UI Automation Control

This skill enables you to control Windows desktop applications through a gRPC-based automation server. It uses **Approach 2: App Structure (LLM-Friendly)** for efficient "See → Think → Act" loops.

## Prerequisites

- **UiAutomationGRPC.Server** running on `localhost:50051`
- **grpccurl** installed (for direct gRPC calls)

## The Loop: See → Think → Act

```
┌─────────────────────────────────────────────────────────┐
│  1. SEE    → GetAppStructure (get full UI as JSON)      │
│  2. THINK  → Analyze JSON, find target element UniqId   │
│  3. ACT    → PerformActionWithStructure (action + new UI)│
│  4. REPEAT → Response includes updated UI, continue     │
└─────────────────────────────────────────────────────────┘
```

## Commands

### Get App Structure (SEE)

Retrieve the complete UI tree of an application.

```bash
grpccurl -plaintext -d '{"app_name": "calc"}' localhost:50051 UiAutomation.UiAutomationService/GetAppStructure
```

**Parameters:**
| Parameter | Description |
|-----------|-------------|
| `app_name` | Process name (e.g., "calc", "notepad") |
| `process_id` | Alternative: use PID instead |
| `use_process_id` | Set `true` to use PID lookup |

**Returns:** `json_structure` containing the UI tree.

### Understanding AppNode

```json
{
  "UniqId": "42,12345",           // ← Use this for actions
  "Name": "Five",                 // Display name
  "UiAutomationId": "num5Button", // Stable identifier
  "ControlType": "ControlType.Button",
  "BoundingRectangle": "x,y,w,h",
  "IsClickable": true,
  "IsVisible": true,
  "Children": [ ... ]
}
```

### Perform Action with Structure (ACT)

**This is the key method for the loop** - performs an action AND returns the updated UI structure.

```bash
grpccurl -plaintext -d '{"runtime_id": "YOUR_UNIQ_ID", "action": 9}' localhost:50051 UiAutomation.UiAutomationService/PerformActionWithStructure
```

**Parameters:**
| Parameter | Description |
|-----------|-------------|
| `runtime_id` | The `UniqId` from the JSON |
| `action` | Action code (see table below) |
| `arguments` | Optional string array |

### Action Reference

| Action | Code | Use Case | Arguments |
|--------|------|----------|-----------|
| **INVOKE** | 0 | Default action (buttons) | - |
| **TOGGLE** | 1 | Checkboxes, switches | - |
| **SET_VALUE** | 4 | Type text | `["text"]` |
| **SET_FOCUS** | 5 | Focus element | - |
| **MoveTo** | 8 | Move mouse to element | - |
| **LeftClick** | 9 | Click (recommended) | - |
| **RightClick** | 10 | Right-click | - |
| **DoubleClick** | 17 | Double-click | - |

## Example: Calculator 9 × 9

```bash
# 1. Open calculator (if not running)
grpccurl -plaintext -d '{"app_name": "calc"}' localhost:50051 UiAutomation.UiAutomationService/OpenApp

# 2. Get structure → Find "Nine" button UniqId
grpccurl -plaintext -d '{"app_name": "calc"}' localhost:50051 UiAutomation.UiAutomationService/GetAppStructure

# 3. Click "9" → Returns updated structure
grpccurl -plaintext -d '{"runtime_id": "42,xxx", "action": 9}' localhost:50051 UiAutomation.UiAutomationService/PerformActionWithStructure

# 4. Click "×" → Find multiply button, click it
grpccurl -plaintext -d '{"runtime_id": "42,yyy", "action": 9}' localhost:50051 UiAutomation.UiAutomationService/PerformActionWithStructure

# 5. Click "9" again
grpccurl -plaintext -d '{"runtime_id": "42,xxx", "action": 9}' localhost:50051 UiAutomation.UiAutomationService/PerformActionWithStructure

# 6. Click "=" → Get result from display element
grpccurl -plaintext -d '{"runtime_id": "42,zzz", "action": 9}' localhost:50051 UiAutomation.UiAutomationService/PerformActionWithStructure
```

## Other Useful Methods

| Method | Description |
|--------|-------------|
| `OpenApp` | Launch application by path/name |
| `CloseApp` | Close by process name |
| `CloseAppByProcessId` | Close by PID |
| `SendKeys` | Send keyboard input |
| `TakeScreenshot` | Capture window/element |

## Troubleshooting

| Issue | Solution |
|-------|----------|
| App not found | Ensure app is running; use OpenApp first |
| Element not found | UniqIds are runtime-specific; re-fetch structure |
| Access denied | Run server with Admin privileges |
| Server unreachable | Verify `UiAutomationGRPC.Server` is running on 50051 |
