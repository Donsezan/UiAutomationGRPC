using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Core;
using Grpc.Core.Interceptors;
using UiAutomation;

namespace UiAutomationGRPC.LLM
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Configuration from environment variables
            var address = Environment.GetEnvironmentVariable("UIAUTOMATION_SERVER_ADDRESS") 
                ?? "https://localhost:50051";
            var authToken = Environment.GetEnvironmentVariable("UIAUTOMATION_AUTH_TOKEN");
            var insecureMode = Environment.GetEnvironmentVariable("UIAUTOMATION_INSECURE_MODE")?.ToLower() == "true";

            // Show warning for insecure mode
            if (insecureMode)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.Error.WriteLine("║  ⚠️  WARNING: INSECURE MODE - CONNECTION IS NOT ENCRYPTED  ⚠️   ║");
                Console.Error.WriteLine("║  This mode should only be used for development/testing.       ║");
                Console.Error.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                // Ensure address uses http for insecure mode
                if (address.StartsWith("https://"))
                {
                    address = address.Replace("https://", "http://");
                }
            }
            else
            {
                // Ensure address uses https for secure mode
                if (address.StartsWith("http://") && !address.StartsWith("https://"))
                {
                    address = address.Replace("http://", "https://");
                }
            }

            try
            {
                // Configure channel options
                var channelOptions = new GrpcChannelOptions();

                if (insecureMode)
                {
                    // Allow HTTP/2 without TLS for development
                    channelOptions.HttpHandler = new SocketsHttpHandler
                    {
                        EnableMultipleHttp2Connections = true
                    };
                }

                using var channel = GrpcChannel.ForAddress(address, channelOptions);
                
                UiAutomationService.UiAutomationServiceClient client;

                // If auth token provided, create client with auth interceptor
                if (!string.IsNullOrEmpty(authToken))
                {
                    var callInvoker = channel.Intercept(metadata =>
                    {
                        metadata.Add("Authorization", $"Bearer {authToken}");
                        return metadata;
                    });
                    client = new UiAutomationService.UiAutomationServiceClient(callInvoker);
                }
                else
                {
                    client = new UiAutomationService.UiAutomationServiceClient(channel);
                }

                var server = new McpServer(client);
                await server.RunAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal Error: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
