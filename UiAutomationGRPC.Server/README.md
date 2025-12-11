# UiAutomationGRPC Service

This service provides a gRPC interface for Windows UI Automation. It runs as a Windows Service using the .NET Generic Host.

## Prerequisites

- .NET Framework 4.7.2 Runtime
- Administrator privileges

## Installation

To install the service, run the following command in an Administrator command prompt or PowerShell:

```powershell
sc create UiAutomationService binPath= "C:\path\to\your\UiAutomationGRPC.Server.exe" start= auto
```

*Note: Replace `C:\path\to\your\UiAutomationGRPC.Server.exe` with the actual absolute path to the executable.*

## Management

### Start Service
```powershell
sc start UiAutomationService
```

### Stop Service
```powershell
sc stop UiAutomationService
```

### Delete Service
```powershell
sc delete UiAutomationService
```

## Logging

The service logs to the Windows Event Viewer under the **Application** log. Look for events from the source specified in the service configuration (defaulting to .NET Runtime or the application name).

## Troubleshooting

- If the service fails to start, check the Windows Event Log for detailed error messages.
- Ensure that port **50051** is not being used by another application.
