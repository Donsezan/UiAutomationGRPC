# Calculator (Windows)

**Open:** `open_app(appName="calc")`

> **UWP startup — both PID and name are unreliable.** `open_app("calc")` returns a launcher PID that exits in < 1 s; the real UWP window has a different PID. `get_app_structure(appName="Calculator")` also fails because it matches process names, not window titles.
> **Correct startup:** `find_element(AutomationId="CalculatorResults")` with no `startRuntimeId` — desktop-wide scan that acts as a ready-probe and gives you the window scope. Retry once if not found immediately.

---

## Display

| AutomationId | Purpose |
|---|---|
| `CalculatorResults` | Currently displayed value — read `Name` property to get the number |
| `CalculatorExpression` | Full expression being built (e.g. "9 × 9 =") |

**Read the result:**
```
find_element(propertyName="AutomationId", propertyValue="CalculatorResults")
get_property(runtimeId=<id>, propertyName="Name")
```

---

## Number Buttons

| AutomationId | Key |
|---|---|
| `num0Button` | 0 |
| `num1Button` | 1 |
| `num2Button` | 2 |
| `num3Button` | 3 |
| `num4Button` | 4 |
| `num5Button` | 5 |
| `num6Button` | 6 |
| `num7Button` | 7 |
| `num8Button` | 8 |
| `num9Button` | 9 |
| `decimalSeparatorButton` | . |

---

## Operators

| AutomationId | Operator |
|---|---|
| `plusButton` | + |
| `minusButton` | − |
| `multiplyButton` | × |
| `divideButton` | ÷ |
| `equalButton` | = |
| `percentButton` | % |

---

## Control Buttons

| AutomationId | Purpose |
|---|---|
| `clearButton` | C — clear all |
| `clearEntryButton` | CE — clear current entry only |
| `backSpaceButton` | ⌫ backspace |
| `invertButton` | ± negate value |
| `squareRootButton` | √ square root |
| `xpower2Button` | x² |

---

## Fast Path: Compute an Expression

```
# 1. Ready-probe — confirms app is loaded; also gives you the CalculatorResults runtimeId
find_element(propertyName="AutomationId", propertyValue="CalculatorResults")
   → retry once if not found (UWP needs a moment after open_app)

# 2-3. First operand
find_element(propertyName="AutomationId", propertyValue="num9Button")
perform_action_with_structure(runtimeId=<id>, action="INVOKE")
   → tree is refreshed; num9Button and multiplyButton runtimeIds are visible in the response

# 4. Operator — reuse runtimeId from the tree returned in step 3
perform_action_with_structure(runtimeId=<multiplyButton_id>, action="INVOKE")
   → tree refreshed again

# 5. Second operand — reuse num9Button runtimeId (same element, stable across actions)
perform_action_with_structure(runtimeId=<num9Button_id>, action="INVOKE")

# 6. Equals — reuse equalButton runtimeId from the tree
perform_action_with_structure(runtimeId=<equalButton_id>, action="INVOKE")

# 7. Read result from the tree returned by step 6, or re-fetch
get_property(runtimeId=<CalculatorResults_id>, propertyName="Name")  → "Display is 81"
```

**Keyboard shortcut (requires explicit focus — UWP does not auto-focus on launch):**
```
# Always pass runtimeId so the UWP window is focused before keys are sent
find_element(propertyName="AutomationId", propertyValue="CalculatorResults")  → <id>
send_keys(runtimeId=<id>, keys="9*9=")
get_property(runtimeId=<id>, propertyName="Name")  → "Display is 81"
```

---

## Switching Modes (Standard / Scientific / Programmer)

The mode switcher is in the navigation pane. Open the hamburger menu first:
```
find_element(propertyName="Name", propertyValue="Open Navigation")
perform_action(runtimeId=<id>, action="INVOKE")
# Then find "Scientific", "Programmer", etc. by Name
find_element(propertyName="Name", propertyValue="Scientific")
perform_action(runtimeId=<id>, action="INVOKE")
```
