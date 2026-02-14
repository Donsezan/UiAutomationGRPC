using Grpc.Core;
using Microsoft.Extensions.Logging;
using UiAutomation;
using UiAutomationGRPC.Server.Handlers;
using UiAutomationGRPC.Server.Helpers;

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

        public UiAutomationService(ILoggerFactory loggerFactory, AppAccessValidator? appAccessValidator = null)
        {
            _elementHandler = new ElementHandler();
            _actionHandler = new ActionHandler();
            _appHandler = new AppLifecycleHandler(appAccessValidator);
            _screenshotHandler = new ScreenshotHandler();
            _structureHandler = new AppStructureHandler(loggerFactory.CreateLogger<AppStructureHandler>(), _actionHandler);
            _reflectionHandler = new ReflectionHandler();
        }

        // Element Operations
        public override Task<ElementResponse> FindElement(FindElementRequest request, ServerCallContext context)
            => _elementHandler.FindElement(request, context);

        public override Task<ElementListResponse> GetChildren(GetChildrenRequest request, ServerCallContext context)
            => _elementHandler.GetChildren(request, context);

        public override Task<GetPropertyResponse> GetProperty(GetPropertyRequest request, ServerCallContext context)
            => _elementHandler.GetProperty(request, context);

        // Action Operations
        public override Task<PerformActionResponse> PerformAction(PerformActionRequest request, ServerCallContext context)
            => _actionHandler.PerformAction(request, context);

        // App Lifecycle Operations
        public override Task<OpenAppResponse> OpenApp(AppRequest request, ServerCallContext context)
            => _appHandler.OpenApp(request, context);

        public override Task<PerformActionResponse> CloseApp(AppRequest request, ServerCallContext context)
            => _appHandler.CloseApp(request, context);

        public override Task<PerformActionResponse> CloseAppByProcessId(CloseAppByProcessIdRequest request, ServerCallContext context)
            => _appHandler.CloseAppByProcessId(request, context);

        public override Task<PerformActionResponse> SendKeys(SendKeysRequest request, ServerCallContext context)
            => _actionHandler.SendKeys(request, context);

        // Screenshot Operations
        public override Task<ScreenshotResponse> TakeScreenshot(ScreenshotRequest request, ServerCallContext context)
            => _screenshotHandler.TakeScreenshot(request, context);

        // App Structure Operations (LLM-friendly)
        public override Task<AppStructureResponse> GetAppStructure(AppStructureRequest request, ServerCallContext context)
            => _structureHandler.GetAppStructure(request, context);

        public override Task<AppStructureResponse> PerformActionWithStructure(PerformActionRequest request, ServerCallContext context)
            => _structureHandler.PerformActionWithStructure(request, context);

        // Reflection API
        public override Task<ReflectionResponse> Reflect(ReflectionRequest request, ServerCallContext context)
            => _reflectionHandler.Reflect(request, context);

        // Cache Management
        public override Task<PerformActionResponse> ClearCache(ClearCacheRequest request, ServerCallContext context)
        {
            int removed;

            if (!string.IsNullOrEmpty(request.AppName))
            {
                removed = Helpers.ElementCache.ClearByName(request.AppName);
            }
            else if (request.ProcessId > 0)
            {
                removed = Helpers.ElementCache.ClearByProcess(request.ProcessId);
            }
            else
            {
                removed = Helpers.ElementCache.Count;
                Helpers.ElementCache.Clear();
            }

            return Task.FromResult(new PerformActionResponse
            {
                Success = true,
                Message = $"Cache cleared. {removed} element(s) removed."
            });
        }
    }
}
