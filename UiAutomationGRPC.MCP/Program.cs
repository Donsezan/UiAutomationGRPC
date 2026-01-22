using Grpc.Net.Client;
using System;
using System.Threading.Tasks;

namespace UiAutomationGRPC.MCP
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // The UiAutomationGRPC server runs on localhost:50051 by default
            using var channel = GrpcChannel.ForAddress("http://localhost:50051");
            var client = new UiAutomation.UiAutomationService.UiAutomationServiceClient(channel);

            var server = new McpServer(client);
            await server.RunAsync();
        }
    }
}
