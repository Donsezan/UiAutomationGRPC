# {App Display Name} (Windows)

**Open:** `open_app(appName="{process_name}")`  
**Structure:** `get_app_structure(appName="{AppDisplayName}", useProcessId=false)`

> Add any important notes here — UWP vs Win32, admin requirement, version differences, etc.

---

## Key Elements

### {Section — e.g. "Main Toolbar", "Document Area", "Status Bar"}

| AutomationId | Name | ControlType | Purpose |
|---|---|---|---|
| `elementId` | Display Name | Button / Edit / Text / etc. | What this element does |

---

## Common Operations

### {Operation Name}
```
1. find_element(propertyName="AutomationId", propertyValue="elementId")
2. perform_action_with_structure(runtimeId=<id>, action="INVOKE")
```

### Type into a field
```
find_element(propertyName="AutomationId", propertyValue="inputFieldId")
perform_action(runtimeId=<id>, action="SET_FOCUS")
send_keys(keys="text to type")
```

### Read a value
```
find_element(propertyName="AutomationId", propertyValue="displayId")
get_property(runtimeId=<id>, propertyName="Name")   # or "Value" for editable fields
```

---

## Notes

- Any quirks, version differences, or gotchas specific to this app.
- Common failure modes and how to recover.
