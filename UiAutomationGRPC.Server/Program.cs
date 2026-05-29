using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Diagnostics;
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

        // Fail-closed warning: token auth with no configured tokens rejects every request.
        if (tokenAuthEnabled)
        {
            var validTokenCount = builder.Configuration.GetSection("Security:ValidTokens").Get<string[]>()?.Length ?? 0;
            if (validTokenCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Config] WARNING: TokenAuthEnabled=true but Security:ValidTokens is empty — ALL requests will be rejected as Unauthenticated.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"[Config] Token Auth: Enabled with {validTokenCount} valid token(s)");
            }
        }

        // Read feature flags
        // Cache:Enabled defaults to true — set to false for dynamic apps where fresh tree parsing is required.
        UiAutomationGRPC.Server.Helpers.ElementCache.Enabled =
            builder.Configuration.GetValue("Features:Cache:Enabled", defaultValue: true);

        Console.WriteLine(UiAutomationGRPC.Server.Helpers.ElementCache.Enabled
            ? "[Config] Cache: Enabled"
            : "[Config] Cache: DISABLED \u2014 every request will parse the live UI tree");

        // Single serialized UI Automation worker. All UIA/input RPCs are marshalled onto
        // one dedicated MTA thread so concurrent clients can't fight over the mouse/keyboard
        // or race the element cache. The queue depth caps backlog before ResourceExhausted.
        var maxQueuedRequests = builder.Configuration.GetValue("Features:MaxQueuedRequests", defaultValue: 32);
        builder.Services.AddSingleton(_ => new UiaExecutor(maxQueuedRequests));
        Console.WriteLine($"[Config] UIA worker: single dedicated MTA thread, max queue depth {maxQueuedRequests}");

        // App-structure tree tuning (GetAppStructure / PerformActionWithStructure — the LLM hot path).
        var appStructureOptions = new AppStructureOptions();
        builder.Configuration.GetSection("Features:AppStructure").Bind(appStructureOptions);
        builder.Services.AddSingleton(appStructureOptions);
        Console.WriteLine(
            $"[Config] AppStructure: maxDepth {appStructureOptions.MaxDepth}, maxNodes {appStructureOptions.MaxNodes}, " +
            $"includeOffscreen {appStructureOptions.IncludeOffscreen}, compactJson {appStructureOptions.CompactJson}");

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

        // Session 0 / interactive-desktop check. A Windows Service runs in Session 0, which has no
        // access to the interactive user desktop — UIA queries and synthesized mouse/keyboard input
        // would silently target an empty/wrong desktop. Only a process in the user's interactive
        // session can actually drive their apps. We can't fix that from inside the process, so detect
        // it and warn loudly (Event Log when running as a service, console otherwise).
        WarnIfNonInteractiveSession(app.Logger);

        // Map gRPC service
        app.MapGrpcService<UiAutomationService>();
        app.MapGrpcReflectionService();

        app.Run();
    }

    /// <summary>
    /// Detects whether the server is running in a non-interactive context (Session 0, the Windows
    /// Service session) and, if so, emits a hard warning. In Session 0 the process cannot see or
    /// drive the interactive user desktop, so UIA reads and synthesized input will not reach the
    /// user's apps. The supported way to run "as a service" is to launch in the user's interactive
    /// session (e.g. a Scheduled Task triggered at logon, or running under the interactive account).
    /// </summary>
    private static void WarnIfNonInteractiveSession(ILogger logger)
    {
        int sessionId;
        try
        {
            sessionId = Process.GetCurrentProcess().SessionId;
        }
        catch
        {
            sessionId = -1; // SessionId unavailable; fall back to UserInteractive alone.
        }

        // Session 0 is the isolated service session. Environment.UserInteractive is false for a
        // service host. Either signal means input/UIA will not reach the interactive desktop.
        var nonInteractive = sessionId == 0 || !Environment.UserInteractive;
        if (!nonInteractive)
            return;

        const string warning =
            "Server is running in a NON-INTERACTIVE session (Session 0 / Windows Service). " +
            "It CANNOT see or drive the interactive user desktop — UIA queries and synthesized " +
            "mouse/keyboard input will target an empty/wrong desktop and most automation will " +
            "silently fail. Run the server in the user's interactive session instead (e.g. a " +
            "Scheduled Task triggered at logon, or under the interactive user account).";

        logger.LogWarning("{Warning} (SessionId={SessionId}, UserInteractive={UserInteractive})",
            warning, sessionId, Environment.UserInteractive);

        if (Environment.UserInteractive)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Session] WARNING: {warning}");
            Console.ForegroundColor = prev;
        }
    }
}
