using System.Net.Http;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
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
    var options = new GrpcChannelOptions();
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
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
