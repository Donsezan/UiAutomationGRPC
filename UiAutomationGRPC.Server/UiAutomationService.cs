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

        // Cache Management
        public override Task<PerformActionResponse> ClearCache(ClearCacheRequest request, ServerCallContext context)
        {
            int removed;

            if (!string.IsNullOrEmpty(request.AppName))
            {
                _logger.LogInformation("ClearCache requested: scope=ByName, AppName='{AppName}'", request.AppName);
                removed = Helpers.ElementCache.ClearByName(request.AppName);
            }
            else if (request.ProcessId > 0)
            {
                _logger.LogInformation("ClearCache requested: scope=ByProcess, PID={ProcessId}", request.ProcessId);
                removed = Helpers.ElementCache.ClearByProcess(request.ProcessId);
            }
            else
            {
                _logger.LogInformation("ClearCache requested: scope=All");
                removed = Helpers.ElementCache.Count;
                Helpers.ElementCache.Clear();
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

