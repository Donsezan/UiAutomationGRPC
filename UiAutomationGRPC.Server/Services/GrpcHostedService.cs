using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Hosting;
using Grpc.Core;
using Grpc.Core.Interceptors;
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
                string authToken = Environment.GetEnvironmentVariable("UIA_AUTH_TOKEN");
                string certPath = Environment.GetEnvironmentVariable("UIA_SERVER_CERT_PATH") ?? "certs/server.crt";
                string keyPath = Environment.GetEnvironmentVariable("UIA_SERVER_KEY_PATH") ?? "certs/server.key";

                ServerCredentials credentials = ServerCredentials.Insecure;
                if (File.Exists(certPath) && File.Exists(keyPath))
                {
                    Console.WriteLine($"Loading certificates from {certPath} and {keyPath}");
                    var serverCert = File.ReadAllText(certPath);
                    var serverKey = File.ReadAllText(keyPath);
                    var keyCertPair = new KeyCertificatePair(serverCert, serverKey);
                    credentials = new SslServerCredentials(new[] { keyCertPair });
                }
                else
                {
                    Console.WriteLine("Certificates not found. Falling back to Insecure connection.");
                }

                var uiService = UiAutomation.UiAutomationService.BindService(new UiAutomationService());
                if (!string.IsNullOrEmpty(authToken))
                {
                    Console.WriteLine("Authentication enabled via UIA_AUTH_TOKEN.");
                    uiService = uiService.Intercept(new AuthInterceptor(authToken));
                }

                var reflectionServiceImpl = new ReflectionServiceImpl(UiAutomation.UiAutomationService.Descriptor, ServerReflection.Descriptor);
                _server = new Grpc.Core.Server
                {
                    Services = {
                        uiService,
                        ServerReflection.BindService(reflectionServiceImpl)
                    },
                    Ports = { new ServerPort("0.0.0.0", 50051, credentials) }
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
