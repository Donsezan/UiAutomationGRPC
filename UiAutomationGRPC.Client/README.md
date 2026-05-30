# UiAutomationGRPC.Client

A runnable, end-to-end **reference** for driving Windows desktop apps through the
**UiAutomationGRPC.Server** with the **UiAutomationGRPC.Library** client SDK. `Program.cs` is a guided
tour that exercises **every** capability of the server and the Library, organised into two parts that
mirror the two ways the system is used:

| Part | Target app | Pattern it demonstrates |
|------|------------|-------------------------|
| **PART 1 — Page Object Model** | Calculator | The structured pattern a **QA suite** uses: fluent `Selector` paths + Page Objects. |
| **PART 2 — See → Think → Act** | Notepad | The loop an **LLM agent** uses: `GetAppStructure` → pick a `RuntimeId` → act. |

> **Why two apps?** Calculator is a **UWP/Store** app, so `OpenApp` returns a launcher PID and
> `GetAppStructure` can't reliably resolve its window — so PART 1 reaches it through the desktop-rooted
> `Selector` API. Notepad is a classic **Win32** app with a real PID, so PART 2 uses it for the
> PID- and structure-based features. This split is itself a lesson: prefer the Selector API for UWP,
> and `GetAppStructure` for Win32.
>
> **Note on launching Notepad:** PART 2 opens Notepad by its **full path**
> (`%SystemRoot%\System32\notepad.exe`). The bare name `notepad` hits a Windows *App Execution Alias*
> that redirects to the packaged Store Notepad, which behaves like UWP (launcher PID 0, not resolvable
> by `GetAppStructure`). Launching the full path runs the real Win32 binary — a useful pattern whenever
> a "classic" app has a Store replacement shadowing its name.

## Prerequisites

1. **UiAutomationGRPC.Server** running (console or Windows Service), listening on `127.0.0.1:50051`
   by default. Run it **as Administrator** — UIA needs elevation for many target apps. It must run in
   the **interactive desktop session** (not Session 0) or input/screenshots won't reach the screen.
2. **Calculator** and **Notepad** installed (both ship with Windows).

## Run it

```powershell
# 1. Start the server (separate terminal)
dotnet run --project ..\UiAutomationGRPC.Server

# 2. Run this tour
dotnet run --project .
```

Each section is isolated — if one step fails (e.g. an app isn't present) the tour logs it and moves on.
Screenshots are written next to the executable (`notepad_edit.png`, `notepad_window.png`,
`notepad_by_pid.png`, `full_screen.png`).

## What each part shows

### PART 1 — Page Object Model (`Calc/Pages/*.cs`)

- `OpenApp` / `CloseApp` lifecycle.
- **Selectors** — lazy descriptions of *how* to find an element, built with a fluent API:
  ```csharp
  var window = new Selector(new PropertyConditions().NameProperty("Calculator"));
  var button = window.Descendants(new PropertyConditions().AutomationIdProperty("num2Button"));
  // ...or the fluent form:
  var close  = window.Descendants().ControlType("Button").NameContain("Close");
  ```
- **Page Objects** — a `CalcPage` whose `ClickTwo().ClickPlus()...` chain queues actions and flushes
  when a value is read (`GetResult`). This separates *how to find* from *what to do*.
- **Element helpers** on any `IAutomationElement`: `GetRuntimeIdAsync`, `GetRectangleAsync`,
  `IsElementExistAsync`, `ClickAsync`, `NameAsync`, … (each resolves the selector chain on demand).
- **Multi-page navigation** — each transition returns the next strongly-typed Page Object, e.g.
  `CalcPage` → `OpenNavigation()` → `CalcNavigationPage` → `OpenSettings()` → `CalcSettingsPage` →
  `ClickBack()` → `CalcPage`. The tour reads the Settings header and build version, then switches
  Standard ⇄ Scientific through the navigation pane. All locators are **real, verified** Calculator
  AutomationIds (`TogglePaneButton`, `SettingsItem`, `BackButton`, `AboutBuildVersion`, …).

### PART 2 — See → Think → Act (Notepad)

| Step | Call | Purpose |
|------|------|---------|
| **See** | `GetAppStructureAsync("notepad")` | Whole UI tree as compact JSON (offscreen nodes filtered, depth/node-capped). |
| **Think** | walk the JSON, read each node's `UniqId` | `UniqId` **is** the `RuntimeId` — the handle for every later call. |
| **Act** | `SendToElementAsync(id, keys)` | Focuses the element *first*, then types — keys land on the right control. |
| **Act + See** | `PerformActionWithStructureAsync(id, action)` | Performs an action **and** returns the refreshed tree in one round-trip. |
| Raw RPCs | `GetChildrenAsync`, `GetPropertyAsync`, `FindElementAsync` | The element-level API the Library is built on. |
| Reflection | `ReflectAsync(ElementSupportedPatterns / ControlTypes)` | Introspect UIA metadata and per-element capabilities. |
| Mouse | `VirtualMouse` `MoveToAsync` / `LeftClickAsync` / `ScrollStepsAsync` | Simulated OS-level cursor input. |
| Screenshots | `TakeElementScreenshotAsync`, `TakeWindowScreenshotAsync` | Element, window-with-highlight, by-PID, and full-screen (PNG bytes). |
| Cache | `ClearCacheAsync(processId)` | Drop one process's cached elements (also by app name, or all). |
| Lifecycle | `CloseAppByProcessIdAsync(pid)` | Close by PID (forcible; skips the unsaved-changes dialog). |

## Connecting (three modes)

```csharp
// Dev / loopback — no TLS:
await using var driver = new UiAutomationDriver("http://127.0.0.1:50051", insecureMode: true);

// Production — HTTPS with an OS-trusted cert + bearer token:
await using var driver = new UiAutomationDriver("https://host:50051", authToken: "<token>");

// Production — HTTPS pinned to a self-signed cert (no OS install needed):
await using var driver = new UiAutomationDriver("https://host:50051",
                                                certificatePath: "server.pfx", authToken: "<token>");
```

See the [Library README](../UiAutomationGRPC.Library/README.md#security-configuration) for full
security configuration.

## Project layout

- **Program.cs** — the two-part tour and small JSON-tree helpers for walking `GetAppStructure` output.
- **Calc/Pages/** — the Page Object Model sample:
  - `BasePageObject.cs` — the fluent action-pipeline base class.
  - `CalcPage.cs` (+ `CalcPageLocators`) — the Calculator screen and its selectors.
  - `CalcNavigationPage.cs` — the hamburger navigation pane (mode items + Settings).
  - `CalcSettingsPage.cs` — the Settings/About page (theme expander, build version, Back).
