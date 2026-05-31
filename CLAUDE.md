# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A gRPC service that exposes Windows UI Automation (the `System.Windows.Automation` / UIA framework) over the network so any gRPC client — scripts, .NET SDK consumers, or LLM agents via MCP — can drive desktop apps remotely. The core loop for agents is **See → Think → Act**: `GetAppStructure` returns the UI tree as JSON, the agent picks an element by `RuntimeId`, and `PerformActionWithStructure` acts and returns the refreshed tree in one call.

## Projects (all target `net8.0-windows`)

> Every project is `net8.0-windows` (the MCP project is plain `net8.0`). The READMEs were corrected from the old ".NET Framework 4.7.2 / .NET 6.0+" claims — keep them in sync if frameworks change.

| Project | Role |
|---------|------|
| `UiAutomationGRPC.Server` | The gRPC service (`Microsoft.NET.Sdk.Web`, Kestrel + HTTP/2). Runs the actual UIA calls. Can run as a console app or Windows Service. |
| `UiAutomationGRPC.Library` | NuGet-packaged .NET client SDK (`UiAutomationDriver`, `VirtualMouse`, `VirtualKeyboard`, fluent `Selector` API). PackageId `UiAutomationGRPC`. |
| `UiAutomationGRPC.AI/MCP` | MCP server (`UiAutomationGRPC.LLM.csproj`) bridging LLM clients to the gRPC server. Configured via `UIAUTOMATION_*` env vars. |
| `UiAutomationGRPC.Client` | Sample console app — Calculator automation reference using the Page Object pattern. |
| `UiAutomationGRPC.Server.Tests` | NUnit tests for the Server's access-control validators. |

There is no top-level solution that contains everything; each project has its own `.sln`. Build the one you need.

## Commands

```powershell
# Build / run the server (default endpoint http://127.0.0.1:50051)
dotnet run --project UiAutomationGRPC.Server

# Run the MCP server (needs the gRPC server already running)
dotnet run --project UiAutomationGRPC.AI/MCP

# Run the sample client
dotnet run --project UiAutomationGRPC.Client

# Tests (NUnit)
dotnet test UiAutomationGRPC.Server.Tests

# Run a single test or fixture
dotnet test UiAutomationGRPC.Server.Tests --filter "FullyQualifiedName~KeyAccessValidatorTests"
dotnet test UiAutomationGRPC.Server.Tests --filter "Name=SpecificTestMethod"
```

Run the server **as Administrator** — UIA access to many target apps requires elevation. Most interactive behavior cannot be validated by unit tests alone; the tests only cover the access-control validators.

## MCP server setup for Claude Code

The MCP server is configured via `.mcp.json` at the repo root. On first open, Claude Code shows a trust prompt — click **Allow**. Verify with `/mcp` in chat.

**Before the first use (or after any code change), build the binary:**

```powershell
dotnet build UiAutomationGRPC.AI/MCP/UiAutomationGRPC.LLM.csproj
```

The binary path in `.mcp.json` is absolute — update it if the repo is cloned to a different location.

> **Why `.mcp.json` and not `settings.json`?** The VS Code extension reads project MCP servers from `.mcp.json`. The global `~/.claude/settings.json` does not accept `mcpServers`.  
> **Why the `.exe` directly and not `dotnet run`?** `dotnet run` triggers NuGet restore on every startup. A stale package source in `NuGet.Config` can time out (~5 s), exceeding the MCP handshake deadline. Invoking the pre-built `.exe` starts the server in under 2 s.

## The proto is the contract — single source of truth

There is now **one** canonical proto: `UiAutomationGRPC.Server/protos/uiautomation.proto` (compiled `GrpcServices="Server"`). The other projects reference that same file by relative path via their `.csproj` `<Protobuf>` items — there is no second copy to keep in sync:
- `UiAutomationGRPC.Library` — `..\UiAutomationGRPC.Server\protos\uiautomation.proto`, `GrpcServices="Client"`, `Access="Public"`, `Link="Protos\uiautomation.proto"`.
- `UiAutomationGRPC.AI/MCP` — `..\..\UiAutomationGRPC.Server\protos\uiautomation.proto`, `GrpcServices="Client"`.

C# types are generated at build time by `Grpc.Tools` — there are no checked-in generated files. **Edit the one proto; all three projects pick it up.**

## Server architecture

`UiAutomationService` ([UiAutomationService.cs](UiAutomationGRPC.Server/UiAutomationService.cs)) is a **thin orchestrator**: nearly every RPC delegates to a handler in `Handlers/`. Put new logic in the handlers, not the service. (The one exception today is `ClearCache`, whose logic lives inline in the service because it only dispatches to static `ElementCache` methods — keep it that way or move it to a handler if it grows.)

- `ElementHandler` — find / get children / get property
- `ActionHandler` — `PerformAction` (UIA patterns like Invoke/Toggle/SetValue, plus simulated mouse/keyboard) and `SendKeys`
- `AppLifecycleHandler` — `OpenApp` / `CloseApp` (by name) / `CloseAppByProcessId` (by PID)
- `AppStructureHandler` — the LLM-friendly JSON tree (`GetAppStructure`, `PerformActionWithStructure`); builds `AppNode` trees recursively
- `ScreenshotHandler`, `ReflectionHandler`

The `ClearCache` RPC (handled inline in the service) flushes the element cache by PID, by app name (`ClearByName`, resolves name→PIDs like `CloseApp`), or entirely.

### RuntimeId + ElementCache is the central mechanism

Elements are addressed across RPCs by a **`RuntimeId`** string (comma-joined `AutomationElement.GetRuntimeId()`). [`ElementCache`](UiAutomationGRPC.Server/Helpers/ElementCache.cs) is a static, thread-safe `ConcurrentDictionary` that:
- Hands back a `RuntimeId` whenever an element is discovered (`CacheElement`), storing locator metadata (PID + AutomationId/Name/ClassName) alongside the COM reference. **Metadata is stored in both modes** — this is what lets a `RuntimeId` resolve on a later call.
- On every lookup (`TryGetLive`), **probes the cached COM reference for liveness** and, if stale, **re-finds** the element by its stored locator properties (AutomationId/Name/ClassName within the same PID), disambiguating duplicate locators by RuntimeId and falling back to a PID-scoped RuntimeId walk for anonymous containers. Dead-and-unfindable entries are evicted.
- `Features:Cache:Enabled` selects the **RuntimeId resolution strategy, not property freshness** (handlers always read `element.Current.*` live). Enabled (default) trusts and reuses persisted handles, re-finding only when stale; disabled re-resolves every element from the live tree on each access (slower, but always reflects the live tree), falling back to the persisted handle only when a scoped re-find finds nothing. **RuntimeId addressing works in both modes** — disabling no longer breaks cross-call addressing or the See→Think→Act loop. Enabled is recommended even for dynamic UIs because it already auto-re-finds obsolete elements; reach for disabled only when you explicitly want a fresh tree search per access.
- `GetAppStructure` and `PerformActionWithStructure` flush a process's cache (`ClearByProcess`) before rebuilding, so the returned tree is always fresh.

`AutomationMapper` translates proto conditions/scopes/patterns ↔ UIA types and produces `ElementResponse`s. New find-by properties must be added to `LookupProperty`.

### Security / access control (three independent layers)

Configured in `appsettings.json`, wired up in [Program.cs](UiAutomationGRPC.Server/Program.cs):

1. **`AppAccessValidator`** — gates `OpenApp` by WhiteList/BlackList (path resolution, `..` traversal blocking, per-app arg filtering, global restricted args).
2. **`InteractionAccessGuard`** — gates *interactions* with already-running processes using the **same** WhiteList/BlackList (resolves a PID's exe path, caches the decision per PID). This covers element ops (Find/GetChildren/GetProperty/PerformAction/Reflect), `GetAppStructure`, **and process termination** (`CloseApp` / `CloseAppByProcessId` — killing a process is treated as an interaction). Active only when `RestrictInteractions` is true **and** a list is configured. Handlers call `InteractionAccessGuard.CheckAccess(guard, processId)` and bail if it returns non-null. Note the per-PID decision cache never expires, so a recycled Windows PID could in theory inherit a stale decision.
3. **`KeyAccessValidator`** — gates `SendKeys` input via `Features:KeyRestrictions` WhiteList/BlackList (substring match for blacklist, exact match for whitelist, plus the `{PLAINTEXT}` token).

Transport security: `TokenAuthInterceptor` (Bearer token, added only when `Security:TokenAuthEnabled`) and `AuditInterceptor` (always on) in `Services/`. TLS is gated by the **`Security:Enabled`** flag — when false (the shipped default) Kestrel listens on plain HTTP regardless of any cert; when true it loads the cert from `Security:CertificatePath` / `Security:CertificatePassword` and **exits the process** if the file is missing. The listen port comes from `Security:Port` (default 50051) in both modes. The server binds to **loopback `127.0.0.1` by default**; set **`Security:AllowRemote=true`** to listen on all interfaces (`0.0.0.0`) — opt-in because the service can launch/kill processes and synthesize input. Note: enabling `TokenAuthEnabled` with an empty `Security:ValidTokens` list rejects *every* request (fail-closed, no startup warning).

> **Config gotcha:** [Program.cs](UiAutomationGRPC.Server/Program.cs) binds the app access lists from **top-level** `WhiteList` / `BlackList` keys and `RestrictInteractions` (as the Server README documents). Key restrictions are read from `Features:KeyRestrictions`. (The old dead `Features:AppRestrictions` and `RateLimiting` blocks were removed from `appsettings.json` in Phase 4 — nothing read them.)

## Client SDK shape

`UiAutomationDriver` (one per server connection, `IAsyncDisposable`) wraps the generated gRPC client and exposes async methods mirroring the RPCs. Connection modes: `insecureMode: true` (HTTP, dev only), HTTPS with OS-trusted cert, or HTTPS with a pinned self-signed cert (`certificatePath` → thumbprint pinning). `authToken` adds the Bearer header to every call. `VirtualMouse` / `VirtualKeyboard` are thin helpers over `PerformAction` / `SendKeys`.

## Logging

The server logs to the **Windows Event Viewer → Application log** (it runs as a Windows Service). Internal diagnostics use `System.Diagnostics.Trace`. There is no console/file log to tail by default.
