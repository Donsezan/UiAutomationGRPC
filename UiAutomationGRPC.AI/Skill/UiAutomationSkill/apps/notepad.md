# Notepad (Windows)

**Open:** `open_app(appName="notepad")`  
**Structure:** `get_app_structure(appName="Notepad", useProcessId=false)`

---

## Key Elements

### Text Area

| AutomationId | ControlType | Notes |
|---|---|---|
| `15` | Document | Windows 11 Notepad (use this — reliable) |
| `RichEditD2DPT15` | Edit | Older Windows 11 builds |

> **Note:** `find_element(propertyName="ControlType", propertyValue="Edit")` does not work — the server expects a ControlType enum, not a plain string. Always use `AutomationId` to locate the text area.

### Title Bar / Window

The window title is `"{filename} - Notepad"` (or `"Untitled - Notepad"` for a new file).
Use `get_app_structure` by name — the window title changes with the open file.

### Menu Bar

Locate menu items by `Name`:

| Name | Contains |
|---|---|
| `File` | New, New window, Open, Save, Save as, Page setup, Print, Exit |
| `Edit` | Undo, Cut, Copy, Paste, Delete, Find, Find next, Replace, Go to, Select all, Time/Date |
| `View` | Zoom in/out, Restore default zoom, Word wrap, Status bar, Font |
| `Help` | View help, Send feedback, About Notepad |

---

## Common Operations

### Type or replace text
```
find_element(propertyName="AutomationId", propertyValue="15")
send_keys(runtimeId=<id>, keys="Hello, World!")
```
> `send_keys` with `runtimeId` focuses the element automatically — no `SET_FOCUS` call needed.

### Set full content (replaces everything)
```
find_element(propertyName="AutomationId", propertyValue="15")
perform_action(runtimeId=<id>, action="SET_VALUE", arguments=["full content here"])
```

### Read current content
```
find_element(propertyName="AutomationId", propertyValue="15")
get_property(runtimeId=<id>, propertyName="Value")
```

### Select all and overwrite
```
send_keys(keys="^a")
send_keys(keys="replacement text")
```

### Type content and save in one call
```
find_element(propertyName="AutomationId", propertyValue="15")  → <edit_id>
send_keys(runtimeId=<edit_id>, keys="your text here^s")
```
Appending `^s` to the keystroke sequence triggers Ctrl+S in the same call — no second `send_keys` needed.
If the file has never been saved, `^s` opens the Save As dialog — handle it as described below.

### Save As
```
send_keys(keys="^+s")
# or via menu:
find_element(propertyName="Name", propertyValue="File")
perform_action(runtimeId=<id>, action="INVOKE")
find_element(propertyName="Name", propertyValue="Save as")
perform_action(runtimeId=<id>, action="INVOKE")
```

### Open File
```
send_keys(keys="^o")
# File Open dialog appears — use get_app_structure to find the filename Edit field and Open button
```

### Find and Replace
```
send_keys(keys="^h")
# Replace dialog — find "Find what" and "Replace with" Edit fields by Name
```

---

## Handling the Save / Open Dialog

Save and Open dialogs are **child windows of the Notepad process**, not separate processes. Always address them by the Notepad PID:
```
get_app_structure(useProcessId=true, processId=<notepad_pid>)
```
> `get_app_structure(appName="Save As")` always fails — the dialog has no independent process name.

Typical elements in the dialog:

| AutomationId | ControlType | Purpose |
|---|---|---|
| `FileNameControlHost` → child Edit (`1001`) | Edit | Filename input |
| `1` | Button | Save / Open |
| `2` | Button | Cancel |

**Set the filename** — `SET_VALUE` does not work on the filename ComboBox. Use `send_keys` instead:
```
# filename field: find the Edit child inside FileNameControlHost (AutomationId "1001")
send_keys(runtimeId=<filename_edit_id>, keys="^aC:\\path\\to\\file")   # ^a clears existing text
perform_action(runtimeId=<save_btn_id>, action="INVOKE")
```

**Verify save succeeded** — check the title bar property (faster than a full tree scan):
```
get_property(runtimeId=<titlebar_id>, propertyName="Name")  → "filename.txt - Notepad"
```
No asterisk prefix means the file is saved.

---

## Notes

- Windows 11 Notepad supports tabs — each tab is a separate document. The active tab's title is shown in the window titlebar.
- `SET_VALUE` on the Edit control replaces the entire content instantly; use `send_keys` to append or type incrementally.
- For large text input, `SET_VALUE` is significantly faster than `send_keys`.
