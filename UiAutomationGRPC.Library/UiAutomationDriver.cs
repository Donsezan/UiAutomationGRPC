using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using UiAutomation;

namespace UiAutomationGRPC.Library;

/// <summary>
/// Driver for interacting with the UI Automation gRPC service.
/// </summary>
public sealed class UiAutomationDriver : IDisposable, IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private bool _disposed;

    /// <summary>
    /// Internal gRPC client.
    /// </summary>
    public UiAutomationService.UiAutomationServiceClient Client { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAutomationDriver"/> class.
    /// </summary>
    /// <param name="address">The address of the gRPC server.</param>
    /// <param name="token">Optional authentication token.</param>
    /// <param name="allowUnsecureTls">If true, skips TLS certificate validation (useful for self-signed certs).</param>
    /// <summary>
    /// Static factory method to create a driver from a configuration file.
    /// </summary>
    /// <param name="configPath">Path to the uiautomation.config.json file.</param>
    /// <returns>A new UiAutomationDriver instance.</returns>
    public static UiAutomationDriver FromConfig(string configPath = "uiautomation.config.json")
    {
        var config = ClientConfig.Load(configPath);
        if (config.Insecure)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("WARNING: Insecure mode is enabled. Communication will not be encrypted.");
            Console.ResetColor();
        }
        else if (config.ServerAddress != null && config.ServerAddress.StartsWith("http://"))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("CRITICAL ERROR: Secure connection requested but ServerAddress uses 'http'.");
            Console.ResetColor();
            throw new InvalidOperationException("Secure connection requested but ServerAddress uses 'http'. Use 'https' for secure connections or set 'Insecure' to true.");
        }

        return new UiAutomationDriver(
            config.ServerAddress ?? (config.Insecure ? "http://127.0.0.1:50051" : "https://127.0.0.1:50051"),
            config.AuthToken,
            config.AllowUnsecureTls);
    }

    public UiAutomationDriver(string address = "http://127.0.0.1:50051", string? token = null, bool allowUnsecureTls = false)
    {
        var channelOptions = new GrpcChannelOptions();

        if (allowUnsecureTls || address.StartsWith("https") || address.Contains(":443"))
        {
            var httpHandler = new HttpClientHandler();
            if (allowUnsecureTls)
            {
                httpHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            channelOptions.HttpHandler = httpHandler;
        }

        _channel = GrpcChannel.ForAddress(address, channelOptions);

        if (!string.IsNullOrEmpty(token))
        {
            var invoker = _channel.Intercept(new ClientAuthInterceptor(token));
            Client = new UiAutomationService.UiAutomationServiceClient(invoker);
        }
        else
        {
            Client = new UiAutomationService.UiAutomationServiceClient(_channel);
        }
    }

    private class ClientAuthInterceptor : Interceptor
    {
        private readonly string _token;
        public ClientAuthInterceptor(string token) => _token = token;

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var metadata = context.Options.Headers ?? new Metadata();
            metadata.Add("x-auth-token", _token);

            var newOptions = context.Options.WithHeaders(metadata);
            var newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, newOptions);

            return continuation(request, newContext);
        }
    }

    #region App Management

    /// <summary>
    /// Opens an application.
    /// </summary>
    /// <param name="appName">The name or path of the application.</param>
    /// <param name="arguments">The arguments to pass to the application.</param>
    /// <returns>A tuple containing success status, message, and process ID.</returns>
    public async Task<(bool Success, string Message, int ProcessId)> OpenAppAsync(string appName, string arguments = "")
    {
        var response = await Client.OpenAppAsync(new AppRequest { AppName = appName, Arguments = arguments });
        return (response.Success, response.Message, response.ProcessId);
    }

    /// <summary>
    /// Closes an application by name.
    /// </summary>
    /// <param name="appName">The name of the application.</param>
    /// <returns>A tuple containing success status and message.</returns>
    public async Task<(bool Success, string Message)> CloseAppAsync(string appName)
    {
        var response = await Client.CloseAppAsync(new AppRequest { AppName = appName });
        return (response.Success, response.Message);
    }

    /// <summary>
    /// Closes an application by process ID.
    /// </summary>
    /// <param name="processId">The process ID of the application.</param>
    /// <returns>A tuple containing success status and message.</returns>
    public async Task<(bool Success, string Message)> CloseAppByProcessIdAsync(int processId)
    {
        var response = await Client.CloseAppByProcessIdAsync(new CloseAppByProcessIdRequest { ProcessId = processId });
        return (response.Success, response.Message);
    }

    #endregion

    #region Element Finding

    /// <summary>
    /// Finds an element based on a condition.
    /// </summary>
    /// <param name="request">The find element request.</param>
    /// <returns>The found element response.</returns>
    public async Task<ElementResponse> FindElementAsync(FindElementRequest request)
        => await Client.FindElementAsync(request);

    /// <summary>
    /// Gets children of an element.
    /// </summary>
    /// <param name="runtimeId">The runtime ID of the parent element.</param>
    /// <returns>A tuple containing success status, message, and list of elements.</returns>
    public async Task<(bool Success, string Message, List<ElementResponse> Elements)> GetChildrenAsync(string runtimeId = "")
    {
        var request = new GetChildrenRequest { RuntimeId = runtimeId ?? "" };
        var response = await Client.GetChildrenAsync(request);
        var elements = response.Elements?.ToList() ?? new List<ElementResponse>();
        return (response.Success, response.Message, elements);
    }

    #endregion

    #region Actions

    /// <summary>
    /// Performs an action on an element.
    /// </summary>
    /// <param name="runtimeId">The runtime ID of the element.</param>
    /// <param name="action">The action type to perform.</param>
    /// <param name="arguments">Optional arguments for the action.</param>
    /// <returns>A tuple containing success status and message.</returns>
    public async Task<(bool Success, string Message)> PerformActionAsync(string runtimeId, ActionType action, params string[] arguments)
    {
        var request = new PerformActionRequest
        {
            RuntimeId = runtimeId,
            Action = action
        };

        if (arguments?.Length > 0)
        {
            request.Arguments.AddRange(arguments);
        }

        var response = await Client.PerformActionAsync(request);
        return (response.Success, response.Message);
    }

    /// <summary>
    /// Sends keys to the active application.
    /// </summary>
    /// <param name="keys">The keys to send.</param>
    /// <param name="wait">Whether to wait for processing.</param>
    /// <returns>A tuple containing success status and message.</returns>
    public async Task<(bool Success, string Message)> SendKeysAsync(string keys, bool wait = true)
    {
        var response = await Client.SendKeysAsync(new SendKeysRequest { Keys = keys, Wait = wait });
        return (response.Success, response.Message);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a property value from an element.
    /// </summary>
    /// <param name="runtimeId">The runtime ID of the element.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>A tuple containing success status, value, and message.</returns>
    public async Task<(bool Success, string Value, string Message)> GetPropertyAsync(string runtimeId, string propertyName)
    {
        var response = await Client.GetPropertyAsync(new GetPropertyRequest
        {
            RuntimeId = runtimeId,
            PropertyName = propertyName
        });
        return (response.Success, response.Value, response.Message);
    }

    #endregion

    #region Screenshots

    /// <summary>
    /// Takes a screenshot of a specific element.
    /// </summary>
    /// <param name="runtimeId">The runtime ID of the element.</param>
    /// <returns>A tuple containing success status, message, and image data.</returns>
    public async Task<(bool Success, string Message, byte[] ImageData)> TakeElementScreenshotAsync(string runtimeId)
    {
        var request = new ScreenshotRequest { Mode = ScreenshotMode.Element, RuntimeId = runtimeId };
        var response = await Client.TakeScreenshotAsync(request);
        return (response.Success, response.Message, response.ImageData.ToByteArray());
    }

    /// <summary>
    /// Takes a screenshot of the window.
    /// </summary>
    /// <param name="runtimeId">Optional runtime ID to highlight.</param>
    /// <param name="processId">Optional process ID to target a specific window.</param>
    /// <returns>A tuple containing success status, message, and image data.</returns>
    public async Task<(bool Success, string Message, byte[] ImageData)> TakeWindowScreenshotAsync(string? runtimeId = null, int? processId = null)
    {
        var request = new ScreenshotRequest { Mode = ScreenshotMode.Window };

        if (!string.IsNullOrEmpty(runtimeId))
            request.RuntimeId = runtimeId;

        if (processId.HasValue)
            request.ProcessId = processId.Value;

        var response = await Client.TakeScreenshotAsync(request);
        return (response.Success, response.Message, response.ImageData.ToByteArray());
    }

    #endregion

    #region Reflection

    /// <summary>
    /// Queries reflection metadata about automation properties, patterns, and control types.
    /// </summary>
    /// <param name="target">The reflection target.</param>
    /// <param name="runtimeId">Optional runtime ID for element-specific queries.</param>
    /// <returns>The reflection response.</returns>
    public async Task<ReflectionResponse> ReflectAsync(ReflectionTarget target, string? runtimeId = null)
    {
        var request = new ReflectionRequest { Target = target };

        if (!string.IsNullOrEmpty(runtimeId))
            request.RuntimeId = runtimeId;

        return await Client.ReflectAsync(request);
    }

    #endregion

    #region App Structure (LLM-Friendly)

    /// <summary>
    /// Gets the application structure as JSON.
    /// </summary>
    /// <param name="appName">The application name.</param>
    /// <param name="processId">The process ID (used if useProcessId is true).</param>
    /// <param name="useProcessId">Whether to use process ID instead of app name.</param>
    /// <param name="arguments">Optional arguments for launching if not open.</param>
    /// <returns>A tuple containing success status, message, and JSON structure.</returns>
    public async Task<(bool Success, string Message, string JsonStructure)> GetAppStructureAsync(
        string appName = "",
        int processId = 0,
        bool useProcessId = false,
        string arguments = "")
    {
        var response = await Client.GetAppStructureAsync(new AppStructureRequest
        {
            AppName = appName,
            ProcessId = processId,
            UseProcessId = useProcessId,
            Arguments = arguments
        });
        return (response.Success, response.Message, response.JsonStructure);
    }

    /// <summary>
    /// Performs an action and returns the updated application structure.
    /// </summary>
    /// <param name="runtimeId">The runtime ID of the element.</param>
    /// <param name="action">The action type to perform.</param>
    /// <param name="arguments">Optional arguments for the action.</param>
    /// <returns>A tuple containing success status, message, and updated JSON structure.</returns>
    public async Task<(bool Success, string Message, string JsonStructure)> PerformActionWithStructureAsync(
        string runtimeId,
        ActionType action,
        params string[] arguments)
    {
        var request = new PerformActionRequest
        {
            RuntimeId = runtimeId,
            Action = action
        };

        if (arguments?.Length > 0)
        {
            request.Arguments.AddRange(arguments);
        }

        var response = await Client.PerformActionWithStructureAsync(request);
        return (response.Success, response.Message, response.JsonStructure);
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the driver and shuts down the channel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _channel.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes the driver and shuts down the channel.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _channel.Dispose();
        await Task.CompletedTask;
    }

    #endregion
}
