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
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Services
{
    public class GrpcHostedService : IHostedService
    {
        private Grpc.Core.Server _server;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var config = ServerConfig.Load();

                string authToken = config.AuthToken;
                string certPath = config.CertificatePath ?? "certs/server.crt";
                string keyPath = config.PrivateKeyPath ?? "certs/server.key";
                string address = config.Address ?? "0.0.0.0:50051";

                ServerCredentials credentials = ServerCredentials.Insecure;

                if (config.Insecure)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("WARNING: Insecure mode is enabled. Communication will not be encrypted.");
                    Console.ResetColor();
                }
                else if (File.Exists(certPath) && File.Exists(keyPath))
                {
                    Console.WriteLine($"Loading certificates from {certPath} and {keyPath}");
                    var serverCert = File.ReadAllText(certPath);
                    var serverKey = File.ReadAllText(keyPath);
                    var keyCertPair = new KeyCertificatePair(serverCert, serverKey);
                    credentials = new SslServerCredentials(new[] { keyCertPair });
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("WARNING: Certificates not found. Falling back to Insecure connection.");
                    Console.ResetColor();
                }

                var uiService = UiAutomation.UiAutomationService.BindService(new UiAutomationService());
                if (!string.IsNullOrEmpty(authToken))
                {
                    Console.WriteLine("Authentication enabled.");
                    uiService = uiService.Intercept(new AuthInterceptor(authToken));
                }

                var reflectionServiceImpl = new ReflectionServiceImpl(UiAutomation.UiAutomationService.Descriptor, ServerReflection.Descriptor);

                string[] hostPort = address.Split(':');
                string host = hostPort[0];
                int port = hostPort.Length > 1 ? int.Parse(hostPort[1]) : 50051;

                _server = new Grpc.Core.Server
                {
                    Services = {
                        uiService,
                        ServerReflection.BindService(reflectionServiceImpl)
                    },
                    Ports = { new ServerPort(host, port, credentials) }
                };
                _server.Start();
                Console.WriteLine($"gRPC Server started on {address}");
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
