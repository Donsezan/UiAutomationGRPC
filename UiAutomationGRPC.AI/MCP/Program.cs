using System.Net.Http;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UiAutomation;

// MCP stdio server (rewritten in Phase 3 onto the official ModelContextProtocol C# SDK).
// stdout is reserved for the JSON-RPC stream, so ALL logging must go to stderr.
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// --- gRPC client configuration (env vars, same contract as before) ---
var address = Environment.GetEnvironmentVariable("UIAUTOMATION_SERVER_ADDRESS")
    ?? "https://localhost:50051";
var authToken = Environment.GetEnvironmentVariable("UIAUTOMATION_AUTH_TOKEN");
var insecureMode = string.Equals(
    Environment.GetEnvironmentVariable("UIAUTOMATION_INSECURE_MODE"),
    "true", StringComparison.OrdinalIgnoreCase);

if (insecureMode && address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    address = "http://" + address.Substring("https://".Length);
else if (!insecureMode && address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
    address = "https://" + address.Substring("http://".Length);

if (insecureMode)
{
    // Loud warning on stderr — never weaken transport silently.
    Console.Error.WriteLine("WARNING: UIAUTOMATION_INSECURE_MODE=true — connection to the " +
        "UiAutomation server is NOT encrypted. Use only for development/testing.");
}

// Register the gRPC client as a singleton. MCP tool methods receive it as a parameter;
// the SDK resolves it from DI and omits it from each tool's JSON schema.
builder.Services.AddSingleton<UiAutomationService.UiAutomationServiceClient>(_ =>
{
    var options = new GrpcChannelOptions
    {
        // Transparent retry for pre-execution rejections only: ResourceExhausted (UIA worker
        // queue full) and Unavailable (connection refused / shutdown). A busy moment then
        // surfaces to the LLM as a short delay instead of a hard tool error. Codes that can
        // occur mid-action are NOT retried — replaying a click is never safe.
        ServiceConfig = new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = 4,
                        InitialBackoff = TimeSpan.FromMilliseconds(200),
                        MaxBackoff = TimeSpan.FromSeconds(2),
                        BackoffMultiplier = 2,
                        RetryableStatusCodes = { StatusCode.ResourceExhausted, StatusCode.Unavailable }
                    }
                }
            }
        }
    };
    if (insecureMode)
    {
        // Allow HTTP/2 without TLS (h2c) for development.
        options.HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };
    }

    var channel = GrpcChannel.ForAddress(address, options);

    if (!string.IsNullOrEmpty(authToken))
    {
        var invoker = channel.Intercept(metadata =>
        {
            metadata.Add("Authorization", $"Bearer {authToken}");
            return metadata;
        });
        return new UiAutomationService.UiAutomationServiceClient(invoker);
    }

    return new UiAutomationService.UiAutomationServiceClient(channel);
});

builder.Services
    .AddMcpServer(options => options.ServerInstructions = """
        Windows UI Automation over gRPC. Core loop (See -> Think -> Act):
        1. open_app, then wait_for_element on a known element (NOT a sleep/retry loop) — for
           UWP/Store apps the returned process_id may be a launcher; prefer addressing by name.
        2. get_app_structure for the UI tree. On large apps pass maxDepth / scopeRuntimeId,
           and after the first look use diffMode=true to receive only what changed.
        3. perform_action_with_structure to act AND get the refreshed tree in one call —
           prefer it over perform_action. Address elements by the runtime_id (UniqId) field.
        Prefer pattern actions (INVOKE, SET_VALUE, TOGGLE) over coordinate clicks; use send_keys
        with a runtime_id so keys land on a specific control. If tools fail mysteriously, call
        get_server_status — a non-interactive (Session 0) server cannot drive the desktop.
        """)
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
