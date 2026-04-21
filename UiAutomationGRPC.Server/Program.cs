using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;
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

        // Read feature flags
        // Cache:Enabled defaults to true — set to false for dynamic apps where fresh tree parsing is required.
        UiAutomationGRPC.Server.Helpers.ElementCache.Enabled =
            builder.Configuration.GetValue("Features:Cache:Enabled", defaultValue: true);

        Console.WriteLine(UiAutomationGRPC.Server.Helpers.ElementCache.Enabled
            ? "[Config] Cache: Enabled"
            : "[Config] Cache: DISABLED \u2014 every request will parse the live UI tree");

        // Bind WhiteList / BlackList application access control
        var appAccessConfig = new AppAccessConfig();
        builder.Configuration.GetSection("WhiteList").Bind(appAccessConfig.WhiteList);
        builder.Configuration.GetSection("BlackList").Bind(appAccessConfig.BlackList);
        appAccessConfig.RestrictInteractions = builder.Configuration.GetValue("RestrictInteractions", defaultValue: true);
        builder.Services.AddSingleton(appAccessConfig);
        builder.Services.AddSingleton<AppAccessValidator>();

        // Register InteractionAccessGuard — gates element interactions using the same whitelist/blacklist
        builder.Services.AddSingleton<InteractionAccessGuard>();

        var hasAppWhiteList = appAccessConfig.WhiteList.Any(w => !string.IsNullOrWhiteSpace(w.Path));
        var hasAppBlackList = appAccessConfig.BlackList.Any(b => !string.IsNullOrWhiteSpace(b.Path));
        if (hasAppWhiteList || hasAppBlackList)
        {
            var listType = hasAppWhiteList ? "WhiteList" : "BlackList";
            var entryCount = hasAppWhiteList ? appAccessConfig.WhiteList.Count : appAccessConfig.BlackList.Count;
            Console.WriteLine($"[Config] App Access: {listType} with {entryCount} entries");
            Console.WriteLine(appAccessConfig.RestrictInteractions
                ? "[Config] Interaction Restrictions: ACTIVE — element interactions restricted to allowed apps"
                : "[Config] Interaction Restrictions: DISABLED — only app launch is restricted");
        }
        else
        {
            Console.WriteLine("[Config] App Access: No restrictions — all applications allowed");
        }

        // Bind SendKeys key restriction configuration
        var keyRestrictionConfig = new KeyRestrictionConfig();
        builder.Configuration.GetSection("Features:KeyRestrictions:WhiteList").Bind(keyRestrictionConfig.WhiteList);
        builder.Configuration.GetSection("Features:KeyRestrictions:BlackList").Bind(keyRestrictionConfig.BlackList);
        builder.Services.AddSingleton(keyRestrictionConfig);
        builder.Services.AddSingleton<KeyAccessValidator>();

        var keyWhiteCount = keyRestrictionConfig.WhiteList.Count;
        var keyBlackCount = keyRestrictionConfig.BlackList.Count;
        Console.WriteLine(keyWhiteCount > 0
            ? $"[Config] Key Restrictions: WhiteList with {keyWhiteCount} entries"
            : keyBlackCount > 0
                ? $"[Config] Key Restrictions: BlackList with {keyBlackCount} entries"
                : "[Config] Key Restrictions: None — all keys allowed");

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
