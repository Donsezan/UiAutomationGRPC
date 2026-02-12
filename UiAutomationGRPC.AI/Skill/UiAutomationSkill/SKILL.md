---
name: Windows UI Automation Control
description: Control Windows applications using UiAutomationGRPC.Server with the "See → Think → Act" loop for efficient LLM-driven UI automation.
---

# Windows UI Automation Control

This skill enables you to control Windows desktop applications through a gRPC-based automation server. It uses **Approach 2: App Structure (LLM-Friendly)** for efficient "See → Think → Act" loops.

## Prerequisites

- **UiAutomationGRPC.Server** running (default: `localhost:50051`)
- **grpccurl** installed (for direct gRPC calls)

## Security Configuration

The server supports two security modes. You must configure `grpccurl` accordingly.

### Insecure Mode (Development/Testing)

When the server is running with `Security.Enabled: false` (default for development):

```bash
# Use -plaintext flag for HTTP connections
grpccurl -plaintext -d '{"app_name": "calc"}' localhost:50051 UiAutomation.UiAutomationService/GetAppStructure
```

### Secure Mode (Production)

When the server is running with `Security.Enabled: true`:

```bash
# Use HTTPS (no -plaintext flag) + TLS certificate verification
grpccurl -cacert server.crt -d '{"app_name": "calc"}' localhost:50051 UiAutomation.UiAutomationService/GetAppStructure

# Or skip certificate verification (not recommended for production)
grpccurl -insecure -d '{"app_name": "calc"}' localhost:50051 UiAutomation.UiAutomationService/GetAppStructure
```

### Token Authentication

When the server has `Security.TokenAuthEnabled: true`, include the authorization header:

```bash
# Add -H for Authorization header with Bearer token
grpccurl -plaintext \
  -H "Authorization: Bearer YOUR_SECRET_TOKEN" \
  -d '{"app_name": "calc"}' \
  localhost:50051 UiAutomation.UiAutomationService/GetAppStructure
```

**Combined Secure + Token Auth:**
```bash
grpccurl -insecure \
  -H "Authorization: Bearer YOUR_SECRET_TOKEN" \
  -d '{"app_name": "calc"}' \
  localhost:50051 UiAutomation.UiAutomationService/GetAppStructure
```

> **Note:** If you receive `Unauthenticated` errors, verify:
> 1. The token matches one in the server's `Security.ValidTokens` array
> 2. The header format is exactly `Authorization: Bearer <token>`

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
| `ClearCache` | Clear element cache (teardown after closing app) |

> **Tip:** After closing an application with `CloseApp` or `CloseAppByProcessId`, call `ClearCache` to free memory and prevent stale element references. This is especially important when the server runs for long periods.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| App not found | Ensure app is running; use OpenApp first |
| Element not found | UniqIds are runtime-specific; re-fetch structure |
| Access denied | Run server with Admin privileges |
| Server unreachable | Verify `UiAutomationGRPC.Server` is running on 50051 |
| SSL connection error | Server uses HTTP; add `-plaintext` flag to grpccurl |
| Certificate error | Use `-insecure` flag or provide valid `-cacert` |
| `Unauthenticated` | Token auth enabled; add `-H "Authorization: Bearer TOKEN"` |
| `Invalid token` | Verify token is in server's `Security.ValidTokens` list |
