# UiAutomationGRPC.Client

This project serves as a comprehensive example (Client) of how to use the **UiAutomationGRPC.Library** to interact with the **UiAutomationGRPC.Server**. It demonstrates how to separate UI automation logic from the server implementation, allowing for remote or local control of Windows UI applications using gRPC.

## Project Structure

- **Program.cs**: The entry point of the application. It contains structured examples of:
  - Connecting to the server (`UiAutomationDriver`).
  - Opening/Closing applications.
  - Interacting with UI elements (Click, Input).
  - taking screenshots.
- **Calc/**: A sample implementation of the **Page Object Model**.
  - **Pages/CalcPage.cs**: Represents the Calculator window and usage logic (e.g., specific workflows like `ClickTwo()`).
  - **Pages/CalcPageLocators.cs**: Defines the **Selectors** used to find elements within the Calculator window. This cleanly separates *how to find* elements from *what to do* with them.

## Prerequisites

1. **UiAutomationGRPC.Server**: The server component must be running.
   - It can be run as a Console Application or a Windows Service.
   - By default, it listens on `127.0.0.1:50051`.
2. **Target Application**: The examples perform automation on the standard Windows Calculator (`calc.exe`). Ensure it is installed.

## Key Concepts

### UiAutomationDriver
The `UiAutomationDriver` is the bridge between your client code and the gRPC server. You initialize it with the server's address.

```csharp
using (var driver = new UiAutomationDriver("127.0.0.1:50051"))
{
    // Use driver to open apps, close apps, or create elements
}
```

### Selectors
Selectors describe **how** to locate an element in the UI tree. They are lazy descriptions, meaning they don't find the element until you actually perform an action (like Click).

**Building a Selector Path:**

You typically start from a root element (like the main Window) and drill down:

```csharp
// 1. Define Root
var window = new Selector(new PropertyConditions().NameProperty("Calculator"));

// 2. Define Child (using Fluent API)
var button = window.Descendants().ControlType("Button").NameContain("Close");

// 3. Or using PropertyConditions directly
var specificButton = window.Descendants(new PropertyConditions().AutomationIdProperty("num2Button"));
```

See `Calc/Pages/CalcPageLocators.cs` for a full example.

### Interactions
Once you have an `IAutomationElement` (created from a Selector + Driver), you can perform actions:
- `.Click()`
- `.SendKey("...")`
- `.GetRuntimeId()` (Useful for screenshots)

## Running the Examples
1. Start `UiAutomationGRPC.Server`.
2. Run `UiAutomationGRPC.Client`.
3. Watch the Calculator open and perform unrelated math operations automatically!
