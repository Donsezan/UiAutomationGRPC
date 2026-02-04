using Grpc.Core;
using System;
using System.Threading.Tasks;
using UiAutomationGRPC.LayerServer.Services;
using UiAutomation;

namespace UiAutomationGRPC.LayerServer
{
    class Program
    {
        const int Port = 50052;

        static void Main(string[] args)
        {
            Server server = new Server
            {
                Services = { UiAutomationService.BindService(new LayerService()) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };

            try
            {
                server.Start();

                Console.WriteLine("UiAutomationGRPC.LayerServer listening on port " + Port);
                Console.WriteLine("Press any key to stop the server...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server failed to start: {ex.Message}");
                throw;
            }
            finally
            {
                if (server != null)
                {
                    server.ShutdownAsync().Wait();
                }
            }
        }
    }
}
