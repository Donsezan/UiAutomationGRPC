# UiAutomationGRPC.Library

A client library for remote Windows UI Automation using gRPC. This library allows you to control Windows applications remotely, perform UI interactions, and take screenshots via a driver-server architecture.

## Features

- **Remote Automation**: Control applications on a remote machine running the UiAutomationGRPC Server.
- **Fluent API**: Intuitive selector syntax for finding UI elements.
- **App Management**: Open and close applications by name or process ID.
- **Element Interactions**: Click, type text, invoke, toggle, and more.
- **Screenshots**: Capture screenshots of specific elements or the entire window.
- **Keyboard & Mouse**: Full virtual keyboard and mouse support via helper classes.
- **App Structure**: LLM-friendly JSON representation of the application UI tree.

## Installation

Install the package via NuGet:

```bash
dotnet add package UiAutomationGRPC
```

## Prerequisites

- **.NET 6.0+** runtime
- **UiAutomationGRPC Server**: The target machine must be running the [UiAutomationGRPC Server](https://github.com/Donsezan/UiAutomationGRPC).

## Quick Start

### 1. Initialize the Driver

```csharp
using UiAutomationGRPC.Library;

// Connect to the gRPC server (insecure mode for development)
await using var driver = new UiAutomationDriver("http://127.0.0.1:50051", insecureMode: true);

// Or with authentication
await using var driver = new UiAutomationDriver("https://127.0.0.1:50051", authToken: "your-token");
```

### 2. Open an Application

```csharp
var (success, message, processId) = await driver.OpenAppAsync("calc");
Console.WriteLine($"Calculator opened with PID: {processId}");
```

### 3. Find Elements and Perform Actions

```csharp
// Find an element
var findRequest = new FindElementRequest
{
    Condition = new Condition
    {
        PropertyCondition = new PropertyCondition
        {
            PropertyName = "AutomationId",
            PropertyValue = "num9Button"
        }
    },
    Scope = TreeScope.Descendants
};
var element = await driver.FindElementAsync(findRequest);

// Perform an action
await driver.PerformActionAsync(element.RuntimeId, ActionType.Invoke);
```

### 4. Use Virtual Keyboard & Mouse

```csharp
using UiAutomationGRPC.Library.Helpers;

var keyboard = new VirtualKeyboard(driver);
var mouse = new VirtualMouse(driver);

// Type text
await keyboard.SendWaitAsync("2+2=");

// Click on an element
await mouse.LeftClickAsync(element.RuntimeId);
```

### 5. Take Screenshots

```csharp
var (_, _, imageData) = await driver.TakeWindowScreenshotAsync(processId: processId);
File.WriteAllBytes("screenshot.png", imageData);
```

### 6. Use App Structure (LLM-Friendly)

```csharp
// Get full UI tree as JSON
var (success, message, json) = await driver.GetAppStructureAsync(appName: "calc");

// Perform action and get updated structure
var (_, _, updatedJson) = await driver.PerformActionWithStructureAsync(
    runtimeId: "42,12345",
    action: ActionType.LeftClick);
```

### 7. Close Application

```csharp
await driver.CloseAppByProcessIdAsync(processId);
```

## API Reference

### UiAutomationDriver

| Method | Description |
|--------|-------------|
| `OpenAppAsync` | Launch an application |
| `CloseAppAsync` / `CloseAppByProcessIdAsync` | Close an application |
| `FindElementAsync` | Find an element by conditions |
| `GetChildrenAsync` | Get child elements |
| `PerformActionAsync` | Perform action (Invoke, Click, Toggle, etc.) |
| `SendKeysAsync` | Send keyboard input |
| `GetPropertyAsync` | Get element property value |
| `TakeElementScreenshotAsync` / `TakeWindowScreenshotAsync` | Capture screenshots |
| `ReflectAsync` | Query automation metadata |
| `GetAppStructureAsync` | Get application structure as JSON |
| `PerformActionWithStructureAsync` | Perform action and get updated structure |

### VirtualMouse

| Method | Description |
|--------|-------------|
| `MoveAsync` | Move cursor by relative delta |
| `MoveToAsync` | Move cursor to element's clickable point |
| `LeftClickAsync` | Left click (at position or on element) |
| `LeftDownAsync` / `LeftUpAsync` | Press/release left button |
| `RightClickAsync` | Right click (at position or on element) |
| `RightDownAsync` / `RightUpAsync` | Press/release right button |
| `DoubleClickAsync` | Double left click |
| `MiddleClickAsync` | Middle mouse button click |
| `ScrollAsync` | Scroll mouse wheel (raw delta) |
| `ScrollStepsAsync` | Scroll by steps (1 step = 120 units) |

### VirtualKeyboard

| Method | Description |
|--------|-------------|
| `SendAsync` | Send keys without waiting |
| `SendWaitAsync` | Send keys and wait for processing |
| `SendKeyAsync` | Send single key with delay |

## License

Apache-2.0
