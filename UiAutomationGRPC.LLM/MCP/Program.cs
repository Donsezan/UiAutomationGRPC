using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using UiAutomation;

namespace UiAutomationGRPC.LLM
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Allow http connections for gRPC
            // AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            // The LayerServer listens on 50052
            var address = "http://localhost:50052";
            
            try 
            {
                using var channel = GrpcChannel.ForAddress(address);
                var client = new UiAutomationService.UiAutomationServiceClient(channel);
                
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
