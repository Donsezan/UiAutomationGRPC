# UiAutomationGRPC

A client library for remote Windows UI Automation using gRPC. This library allows you to control Windows applications remotely, perform UI interactions, and take screenshots via a driver-server architecture.

## Features

- **Remote Automation**: Control applications on a remote machine running the UiAutomationGRPC Server.
- **Fluent API**: intuitive selector syntax for finding UI elements.
- **App Management**: Open and close applications by name or process ID.
- **Interactions**: Click, type text, and simulate keyboard inputs.
- **Screenshots**: Capture screenshots of specific elements or the entire window/screen.

## Installation

Install the package via NuGet:

```bash
dotnet add package UiAutomationGRPC
```

## Prerequisites

- **UiAutomationGRPC Server**: The target machine must be running the [UiAutomationGRPC Server](https://github.com/Donsezan/UiAutomationGRPC).

## Usage

### 1. Initialize the Driver

Connect to the running gRPC server.

```csharp
using UiAutomationGRPC.Library;

// Connect to localhost or remote IP
using var driver = new UiAutomationDriver("127.0.0.1:50051");
```

### 2. Open an Application

```csharp
var openResult = await driver.OpenApp("calc");
if (openResult.Success)
{
    Console.WriteLine($"Calculator opened with PID: {openResult.ProcessId}");
}
```

### 3. Find Elements and Interact

Use the fluent selector API to define elements and perform actions.

```csharp
// Define a selector for the "Two" button
var buttonTwo = new Selector()
    .ProcessId(processId)
    .ControlType("Button")
    .Name("Two");

// Click the button
var clickResult = await driver.Click(buttonTwo);

// Or using a higher level Page Object pattern (recommended)
// ...
```

### 4. Keyboard Input

```csharp
// Send text or keys
await driver.SendKeys("2+2=");
```

### 5. Take Screenshots

```csharp
// Screenshot a specific element by Runtime ID
var elementScreenshot = await driver.TakeElementScreenshot(elementRuntimeId);
File.WriteAllBytes("element.png", elementScreenshot.ImageData);

// Screenshot the active window
var windowScreenshot = await driver.TakeWindowScreenshot();
File.WriteAllBytes("window.png", windowScreenshot.ImageData);
```

### 6. Close Application

```csharp
// Close by name
driver.CloseApp("CalculatorApp");

// Close by Process ID
driver.CloseAppByProcessId(processId);
```
