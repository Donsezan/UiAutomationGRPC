using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using UiAutomationGRPC.Server.Services;

namespace UiAutomationGRPC.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Add configuration
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        
        // Read security settings
        var securityEnabled = builder.Configuration.GetValue<bool>("Security:Enabled");
        var port = builder.Configuration.GetValue<int>("Security:Port", 50051);
        var certPath = builder.Configuration.GetValue<string>("Security:CertificatePath") ?? "";
        var certPassword = builder.Configuration.GetValue<string>("Security:CertificatePassword") ?? "";
        var tokenAuthEnabled = builder.Configuration.GetValue<bool>("Security:TokenAuthEnabled");

        // Configure Kestrel
        builder.WebHost.ConfigureKestrel(options =>
        {
            if (securityEnabled)
            {
                // HTTPS mode
                if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
                {
                    var cert = new X509Certificate2(certPath, certPassword);
                    options.Listen(IPAddress.Any, port, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                        listenOptions.UseHttps(cert);
                    });
                    Console.WriteLine($"╔══════════════════════════════════════════════════════════════════╗");
                    Console.WriteLine($"║  ✓ SECURE MODE: gRPC Server listening on https://0.0.0.0:{port}  ");
                    Console.WriteLine($"╚══════════════════════════════════════════════════════════════════╝");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"╔═══════════════════════════════════════════════════════════════╗");
                    Console.WriteLine($"║  ✗ ERROR: Security enabled but certificate not found!         ║");
                    Console.WriteLine($"║  Certificate path: {certPath,-43} ");
                    Console.WriteLine($"╚═══════════════════════════════════════════════════════════════╝");
                    Console.ResetColor();
                    Environment.Exit(1);
                }
            }
            else
            {
                // HTTP mode - insecure with warning
                options.Listen(IPAddress.Any, port, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"║  !! WARNING: INSECURE MODE - CONNECTION IS NOT ENCRYPTED !!  ║");
                Console.WriteLine($"║  This mode should only be used for development/testing.      ║");
                Console.WriteLine($"║  gRPC Server listening on http://0.0.0.0:{port,-22}          ");
                Console.WriteLine($"╚═══════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
            }
        });

        // Add gRPC services
        builder.Services.AddGrpc(options =>
        {
            // Add interceptors for token auth and auditing
            if (tokenAuthEnabled)
            {
                options.Interceptors.Add<TokenAuthInterceptor>();
            }
            options.Interceptors.Add<AuditInterceptor>();
        });

        // Add gRPC reflection for service discovery (required for MapGrpcReflectionService)
        builder.Services.AddGrpcReflection();

        // Support for running as Windows Service
        builder.Host.UseWindowsService();

        var app = builder.Build();

        // Map gRPC service
        app.MapGrpcService<UiAutomationService>();
        app.MapGrpcReflectionService();

        app.Run();
    }
}
