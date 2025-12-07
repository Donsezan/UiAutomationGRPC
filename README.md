# UiAutomationGRPC

A generic gRPC-based Windows UI Automation service and client. This project allows you to drive Windows UI Automation remotely or locally via gRPC messages, enabling cross-language or distributed UI testing and automation.

## Project Overview

- **Server**: A Windows Service (or console app) that hosts a gRPC server. It exposes endpoints to find UI elements, query properties, perform actions (Click, Invoke, etc.), and manage application processes.
- **Client**: A sample .NET client that connects to the server to demonstrate automation tasks, such as opening an application, finding elements, and performing interactions.

## Prerequisites

- **OS**: Windows (Required for UI Automation libraries)
- **Runtime**: .NET Framework 4.7.2 or later

## Getting Started

### 1. Build the Solution

The project uses SDK-style projects targeting .NET Framework 4.7.2. You can build it using Visual Studio or the .NET CLI.

```powershell
# Build the Server
cd UiAutomationGRPC.Server
dotnet build

# Build the Client
cd ../UiAutomationGRPC.Client
dotnet build
```

### 2. Run the Server

Navigate to the server output directory (e.g., `bin/Debug/net472`) and run the executable.

```powershell
.\UiAutomationGRPC.Server.exe
```

The server will start and listen for gRPC connections.

### 3. Run the Client

Navigate to the client output directory and run the client.

```powershell
.\UiAutomationGRPC.Client.exe
```

The client will attempt to connect to the server (default: `localhost`) and run the demonstrated automation sequence.

## Project Structure

```
UiAutomationGRPC
├── UiAutomationGRPC.Server    # gRPC Server implementation
│   ├── protos/                # gRPC Service definitions (.proto)
│   ├── MainService.cs         # Service entry point
│   ├── UiAutomationServiceImplementation.cs # Core logic
│   └── UiAutomationGRPC.Server.csproj
├── UiAutomationGRPC.Client    # gRPC Client sample
│   ├── Program.cs             # Client entry point
│   └── UiAutomationGRPC.Client.csproj
└── README.md
```

## API Reference

The service is defined in `uiautomation.proto`.

### Service: `UiAutomationService`

| Method | Description |
| :--- | :--- |
| `FindElement` | Search for a UI element using conditions like Name, AutomationId, or ControlType. |
| `PerformAction` | Interact with an element (Invoke, Click, SetValue, etc.). |
| `GetProperty` | Retrieve a specific property value from an element. |
| `OpenApp` | Launch an application by name or path. |
| `Reflect` | query metadata about supported automation properties and patterns. |

### Key Concepts

- **Runtime ID**: A string handle returned by `FindElement` that uniquely identifies a UI element interface for subsequent calls.
- **Conditions**: The search query language supports `PropertyCondition` (Name=X), `BoolCondition` (AND/OR/NOT), giving you flexibility in finding elements.

## License

See [LICENSE](LICENSE) file.