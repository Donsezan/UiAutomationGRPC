using Grpc.Core;
using Microsoft.Extensions.Logging;
using UiAutomation;
using UiAutomationGRPC.Server.Handlers;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server
{
    /// <summary>
    /// Main gRPC service that delegates to specialized handlers.
    /// This is a thin orchestrator - all logic is in handler classes.
    /// </summary>
    public class UiAutomationService : UiAutomation.UiAutomationService.UiAutomationServiceBase
    {
        private readonly ElementHandler _elementHandler;
        private readonly ActionHandler _actionHandler;
        private readonly AppLifecycleHandler _appHandler;
        private readonly ScreenshotHandler _screenshotHandler;
        private readonly AppStructureHandler _structureHandler;
        private readonly ReflectionHandler _reflectionHandler;
        private readonly UiaExecutor _executor;
        private readonly ILogger<UiAutomationService> _logger;

        public UiAutomationService(
            ILoggerFactory loggerFactory,
            UiaExecutor executor,
            AppAccessValidator? appAccessValidator = null,
            KeyAccessValidator? keyAccessValidator = null,
            InteractionAccessGuard? interactionGuard = null,
            AppStructureOptions? appStructureOptions = null)
        {
            _logger = loggerFactory.CreateLogger<UiAutomationService>();
            _executor = executor;
            _elementHandler = new ElementHandler(interactionGuard);
            _actionHandler = new ActionHandler(keyAccessValidator, interactionGuard);
            _appHandler = new AppLifecycleHandler(loggerFactory.CreateLogger<AppLifecycleHandler>(), appAccessValidator, interactionGuard);
            _screenshotHandler = new ScreenshotHandler(interactionGuard);
            _structureHandler = new AppStructureHandler(loggerFactory.CreateLogger<AppStructureHandler>(), _actionHandler, interactionGuard, appStructureOptions);
            _reflectionHandler = new ReflectionHandler(interactionGuard);
        }

        // UIA / global-input operations are marshalled onto the single UIA worker thread.
        // Process-lifecycle and cache operations below do NOT touch UIA or global input and
        // run directly (CloseApp's WaitForExit would otherwise starve the worker).

        // Element Operations
        public override Task<ElementResponse> FindElement(FindElementRequest request, ServerCallContext context)
            => _executor.RunAsync(() => _elementHandler.FindElement(request, context), context.CancellationToken);

        public override Task<ElementListResponse> GetChildren(GetChildrenRequest request, ServerCallContext context)
            => _executor.RunAsync(() => _elementHandler.GetChildren(request, context), context.CancellationToken);

        // The wait loop deliberately lives OFF the UIA worker: each probe is enqueued as its own
        // short work item and the inter-probe delay runs on the request thread, so a long wait
        // never starves other clients of the single worker.
        public override async Task<ElementResponse> WaitForElement(WaitForElementRequest request, ServerCallContext context)
        {
            var (timeoutMs, pollMs) = WaitPolicy.Normalize(request.TimeoutMs, request.PollIntervalMs);
            var findRequest = new FindElementRequest
            {
                StartRuntimeId = request.StartRuntimeId,
                Condition = request.Condition,
                Scope = request.Scope
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ElementResponse last;
            while (true)
            {
                try
                {
                    last = await _executor.RunAsync(() => _elementHandler.FindElement(findRequest, context), context.CancellationToken);
                    if (last.Success)
                        return last;
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.ResourceExhausted)
                {
                    // Worker queue momentarily full — treat as a missed probe and keep waiting.
                    last = new ElementResponse { Success = false, Message = "UI automation worker busy during probe." };
                }

                if (stopwatch.ElapsedMilliseconds + pollMs > timeoutMs)
                    break;

                await Task.Delay(pollMs, context.CancellationToken);
            }

            last.Message = $"Element did not appear within {timeoutMs} ms ({last.Message})";
            return last;
        }

        public override Task<GetPropertyResponse> GetProperty(GetPropertyRequest request, ServerCallContext context)
            => _executor.RunAsync(() => _elementHandler.GetProperty(request, context), context.CancellationToken);

        // Action Operations
        public override Task<PerformActionResponse> PerformAction(PerformActionRequest request, ServerCallContext context)
            => _executor.RunAsync(() => _actionHandler.PerformAction(request, context), context.CancellationToken);

        // App Lifecycle Operations (no UIA / global input — not marshalled)
        public override Task<OpenAppResponse> OpenApp(AppRequest request, ServerCallContext context)
            => _appHandler.OpenApp(request, context);

        public override Task<PerformActionResponse> CloseApp(AppRequest request, ServerCallContext context)
            => _appHandler.CloseApp(request, context);

        public override Task<PerformActionResponse> CloseAppByProcessId(CloseAppByProcessIdRequest request, ServerCallContext context)
            => _appHandler.CloseAppByProcessId(request, context);

        public override Task<PerformActionResponse> SendKeys(SendKeysRequest request, ServerCallContext context)
            => _executor.RunAsync(() => _actionHandler.SendKeys(request, context), context.CancellationToken);

        // Screenshot Operations
        public override Task<ScreenshotResponse> TakeScreenshot(ScreenshotRequest request, ServerCallContext context)
            => _executor.RunAsync(() => _screenshotHandler.TakeScreenshot(request, context), context.CancellationToken);

        // App Structure Operations (LLM-friendly)
        public override Task<AppStructureResponse> GetAppStructure(AppStructureRequest request, ServerCallContext context)
            => _executor.RunAsync(() => _structureHandler.GetAppStructure(request, context), context.CancellationToken);

        public override Task<AppStructureResponse> PerformActionWithStructure(PerformActionRequest request, ServerCallContext context)
            => _executor.RunAsync(() => _structureHandler.PerformActionWithStructure(request, context), context.CancellationToken);

        // Reflection API
        public override Task<ReflectionResponse> Reflect(ReflectionRequest request, ServerCallContext context)
            => _executor.RunAsync(() => _reflectionHandler.Reflect(request, context), context.CancellationToken);

        // Server Status (no UIA — answered directly, even when the worker is saturated)
        public override Task<ServerStatusResponse> GetServerStatus(ServerStatusRequest request, ServerCallContext context)
        {
            int sessionId;
            try { sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId; }
            catch { sessionId = -1; }

            bool interactive = sessionId != 0 && Environment.UserInteractive;
            string message = interactive
                ? "OK"
                : "WARNING: server runs in a non-interactive session (Session 0 / service). " +
                  "It cannot see or drive the user desktop — UIA reads and synthesized input will not work. " +
                  "Run the server in the user's interactive session.";

            return Task.FromResult(new ServerStatusResponse
            {
                Success = true,
                Message = message,
                PendingRequests = _executor.Pending,
                QueueCapacity = _executor.MaxQueueDepth,
                CachedElements = Helpers.ElementCache.Count,
                CacheEnabled = Helpers.ElementCache.Enabled,
                SessionId = sessionId,
                InteractiveSession = interactive,
                ServerVersion = typeof(UiAutomationService).Assembly.GetName().Version?.ToString() ?? "unknown"
            });
        }

        // Cache Management
        public override Task<PerformActionResponse> ClearCache(ClearCacheRequest request, ServerCallContext context)
        {
            int removed;

            if (!string.IsNullOrEmpty(request.AppName))
            {
                _logger.LogInformation("ClearCache requested: scope=ByName, AppName='{AppName}'", request.AppName);
                removed = Helpers.ElementCache.ClearByName(request.AppName);

                string name = Helpers.ElementCache.StripExeExtension(request.AppName);
                foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                    Helpers.StructureSnapshotStore.ClearByProcess(p.Id);
            }
            else if (request.ProcessId > 0)
            {
                _logger.LogInformation("ClearCache requested: scope=ByProcess, PID={ProcessId}", request.ProcessId);
                removed = Helpers.ElementCache.ClearByProcess(request.ProcessId);
                Helpers.StructureSnapshotStore.ClearByProcess(request.ProcessId);
            }
            else
            {
                _logger.LogInformation("ClearCache requested: scope=All");
                removed = Helpers.ElementCache.Count;
                Helpers.ElementCache.Clear();
                Helpers.StructureSnapshotStore.Clear();
            }

            _logger.LogInformation("ClearCache completed: {Removed} element(s) removed", removed);

            return Task.FromResult(new PerformActionResponse
            {
                Success = true,
                Message = $"Cache cleared. {removed} element(s) removed."
            });
        }
    }
}

