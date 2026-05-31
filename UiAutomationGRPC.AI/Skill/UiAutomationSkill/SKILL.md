---
name: Windows UI Automation Control
description: Drive Windows desktop apps via the uiautomation MCP server. Uses the See → Think → Act loop. Loads per-app mental maps on demand for fast, targeted navigation.
---

# Windows UI Automation Control

Use the **uiautomation** MCP server to control Windows desktop apps. The server must be running on `localhost:50051` before calling any tool.

---

## Step 0 — Load the App Mental Map

Before navigating any app, check whether a mental map exists for it.
If one does, **read it first** using the `Read` tool — it gives you stable `AutomationId`s so you can skip full tree scans.

Available maps:

| App | Map file (relative to workspace root) |
|-----|---------------------------------------|
| Calculator | `UiAutomationGRPC.AI/Skill/UiAutomationSkill/apps/calculator.md` |
| Notepad | `UiAutomationGRPC.AI/Skill/UiAutomationSkill/apps/notepad.md` |

To load a map:
```
Read("UiAutomationGRPC.AI/Skill/UiAutomationSkill/apps/{appname}.md")
```

If no map exists for the target app, use `get_app_structure` to discover the UI, then consider creating a new map from the template at `UiAutomationGRPC.AI/Skill/UiAutomationSkill/apps/_template.md`.

---

## See → Think → Act Loop

```
SEE    →  get_app_structure            — get full UI tree as compact JSON
THINK  →  locate target by AutomationId / Name / RuntimeId
ACT    →  perform_action_with_structure — execute action + get refreshed UI in one call
REPEAT →  updated tree comes back; continue from new state
```

**Always prefer `perform_action_with_structure` over `perform_action`** — one call does both the action and the next See step.

---

## Addressing Elements

Each UI element has a `UniqId` / `RuntimeId` (e.g. `"42,12345"`) — use it to target any tool.

**Fast path** — when you know the `AutomationId` from a mental map:
```
find_element(propertyName="AutomationId", propertyValue="num9Button")
→ returns runtimeId
perform_action_with_structure(runtimeId=<id>, action="INVOKE")
```

**Discovery path** — unknown app, no mental map:
```
get_app_structure(appName="calc", useProcessId=false)
→ parse JSON, find element by Name + ControlType
→ use UniqId as runtimeId for subsequent calls
```

Prefer elements whose `UiAutomationId` is non-empty — those IDs are stable across sessions.

**Reuse RuntimeIds from the refreshed tree** — `perform_action_with_structure` returns the full updated UI tree after every action. Extract RuntimeIds for subsequent steps from that response instead of issuing redundant `find_element` calls.

---

## MCP Tool Reference

| Tool | Purpose |
|------|---------|
| `open_app` | Launch app by executable name or path |
| `close_app` | Terminate by process id |
| `get_app_structure` | Full UI tree as JSON (See step; auto-flushes stale cache) |
| `find_element` | Locate one element by `AutomationId`, `Name`, `ClassName`, or `ControlType` |
| `get_children` | List direct children of an element (or desktop root if `runtimeId` is empty) |
| `get_property` | Read one property: `Name`, `IsEnabled`, `Value`, etc. |
| `perform_action` | Execute action without refreshing the tree |
| `perform_action_with_structure` | Execute action **and** return refreshed UI — preferred |
| `send_keys` | Keyboard input using SendKeys syntax (`{ENTER}`, `^a`, `+{F4}`) |
| `take_screenshot` | Capture element or window as PNG — use to verify visual state |
| `clear_cache` | Flush server element cache (all / by PID / by app name) |

All tool parameters are **camelCase** (`appName`, `runtimeId`, `useProcessId`, `processId`).

---

## Action Reference

| Action string | Use case | `arguments` |
|---------------|----------|-------------|
| `INVOKE` | Default button click (UIA Invoke pattern) | — |
| `TOGGLE` | Checkboxes, toggle switches | — |
| `SET_VALUE` | Type into an input field | `["text to set"]` |
| `SET_FOCUS` | Focus element (before `send_keys`) | — |
| `EXPAND_COLLAPSE` | Open/close a combo box or tree node | — |
| `SELECT` | Select a list/tab item | — |
| `LeftClick` | Mouse click (use when `INVOKE` does nothing) | optional `["x", "y"]` to click at screen coordinates with no element |
| `RightClick` | Open context menu | optional `["x", "y"]` to click at screen coordinates with no element |
| `DoubleClick` | Double-click (open/edit items) | `["x", "y"]` required when no `runtimeId` is given |
| `MoveTo` | Hover (trigger tooltips, hover states) | — |

> **Coordinate clicks (UIA-opaque controls):** for controls UIA cannot address — custom-drawn tabs, owner-drawn lists, DirectX/canvas surfaces — call `perform_action` with an **empty `runtimeId`** and pass screen coordinates as `arguments`, e.g. `perform_action(action="LeftClick", arguments=["938", "238"])`. Read the target's bounding box from `get_app_structure` (it reports `BoundingRectangle` as `x,y,w,h`) and click its center. Prefer a real UIA element + `INVOKE`/`LeftClick` whenever one exists — coordinates break if the layout shifts.

> **Menu bar items in non-standard apps (MFC, WinForms, Qt, etc.):** Top-level `ControlType.MenuItem` elements typically do not implement the UIA Invoke pattern — `INVOKE` will fail with "Element does not support pattern". Use `LeftClick` directly. Do not try `INVOKE` first.

---

## Navigation Strategy

### Known app — mental map available
1. Read the app's map file.
2. Use `find_element` with stable `AutomationId`s from the map.
3. Act directly — call `get_app_structure` only when state is ambiguous.
4. Call `take_screenshot` to verify visually if unsure.

### After clicking a menu item
Always call `take_screenshot` (or `get_app_structure`) before searching for child elements — the click may have opened a **dropdown** instead of navigating directly. If a dropdown appeared:
- `get_app_structure` returns the process's **main window** tree even while a dropdown is open; the open dropdown's items appear within that tree (or as a sibling popup), so search there for your target.
- If the dropdown is not what you want, press `send_keys(keys="{ESC}")` to dismiss it, then re-query.

### Unknown app — no mental map
1. `open_app` if the app is not already running.
2. `get_app_structure` to see the full UI tree.
3. Navigate by `Name` + `ControlType`; prefer any non-empty `UiAutomationId`.
4. After exploring, consider writing a new `apps/{appname}.md` from the template.

### Dynamic UIs (live data grids, real-time feeds)
- `get_app_structure` always flushes stale cache before returning — safe to call repeatedly.
- For elements that appear/disappear: re-call `find_element` after each action instead of caching `runtimeId` across steps.
- `clear_cache(appName="...")` if elements become consistently unresolvable.

### UWP / Store Apps (Calculator, Paint 3D, etc.)
`open_app` returns a launcher PID that exits in < 1 s; the real window runs under a different PID. Both `get_app_structure(appName=...)` and `get_app_structure(useProcessId=true, processId=<launcher_pid>)` will fail.

Reliable startup sequence:
1. `open_app(appName="calc")` — ignore the returned PID
2. `find_element(propertyName="AutomationId", propertyValue=<known_stable_id>)` — desktop-wide ready-probe; retry once if not found (UWP needs a moment to appear)
3. Use RuntimeIds from that response for all subsequent scoped searches
4. For `send_keys`: always pass `runtimeId` so the UWP window is focused first — UWP apps do not automatically receive keyboard focus after launch

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| App not found | Ensure it is running; use `open_app` first |
| Element not found | RuntimeIds are session-specific; re-fetch structure or re-run `find_element` |
| Action has no effect | Try `LeftClick` instead of `INVOKE`; check `IsEnabled` via `get_property` |
| `INVOKE` fails on menu item | Non-standard apps don't expose Invoke pattern on MenuItems — use `LeftClick` directly |
| Access denied | The server must be running as Administrator |
| MCP server not responding | Start `UiAutomationGRPC.Server` on port 50051 first |
| Screenshot blank | Element may be off-screen or minimized; switch to `mode="window"` |
| Cache stale after app restart | Call `clear_cache(appName="...")` then retry |
| UWP app not found after `open_app` (Calculator, etc.) | Ignore returned PID; use `find_element(AutomationId=<known_id>)` with no `startRuntimeId` for a desktop-wide scan |
| `send_keys` has no visible effect | App may lack focus — pass `runtimeId` of a window element to focus it before sending keys |
| Tab not found by Name | App uses custom-drawn tabs — they appear as unnamed `ControlType.Image` controls (or no UIA element at all). `LeftClick` the Image control by its `runtimeId` if one exists; otherwise click its `BoundingRectangle` center directly with `perform_action(action="LeftClick", arguments=["x", "y"])` |
| `get_app_structure` returns only a small Pane | The server now prefers the process's real top-level `Window`, so a transient dropdown no longer hijacks the result. If you still see only a Pane, the app's real window genuinely is a Pane (e.g. an Electron/Chromium shell) — that is the correct root |
| Closing app returns an error | The server now returns `success` with the message "Action succeeded; process exited during response." and a null tree when a process exits mid-action (e.g. Close button). No recovery call is needed — treat that message as confirmation the app closed |
