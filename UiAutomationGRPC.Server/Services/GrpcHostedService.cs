using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Grpc.Core;
using Grpc.Reflection;
using Grpc.Reflection.V1Alpha;
using UiAutomation;

namespace UiAutomationGRPC.Server.Services
{
    public class GrpcHostedService : IHostedService
    {
        private Grpc.Core.Server _server;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var reflectionServiceImpl = new ReflectionServiceImpl(UiAutomation.UiAutomationService.Descriptor, ServerReflection.Descriptor);
                _server = new Grpc.Core.Server
                {
                    Services = {
                        UiAutomation.UiAutomationService.BindService(new UiAutomationService()),
                        ServerReflection.BindService(reflectionServiceImpl)
                    },
                    Ports = { new ServerPort("127.0.0.1", 50051, ServerCredentials.Insecure) }
                };
                _server.Start();
                Console.WriteLine("gRPC Server started on port 50051");
            }
            catch (Exception ex)
            {
                // Exceptions here will be caught by the Host and logged if a logger is configured
                Console.Error.WriteLine($"Failed to start gRPC server: {ex}");
                throw;
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stopping gRPC Server...");
            if (_server != null)
            {
                await _server.ShutdownAsync();
            }
        }
    }
}
