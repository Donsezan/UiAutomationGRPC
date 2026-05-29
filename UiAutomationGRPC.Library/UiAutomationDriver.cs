using Grpc.Net.Client;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using UiAutomation;

namespace UiAutomationGRPC.Library;

/// <summary>
/// Driver for interacting with the UI Automation gRPC service.
/// </summary>
public sealed class UiAutomationDriver : IDisposable, IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly CallInvoker? _callInvoker;
    private bool _disposed;
    private readonly bool _insecureMode;
    private readonly string? _authToken;
    private readonly ILogger<UiAutomationDriver>? _logger;

    /// <summary>
    /// Internal gRPC client.
    /// </summary>
    public UiAutomationService.UiAutomationServiceClient Client { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAutomationDriver"/> class.
    /// </summary>
    /// <param name="address">The address of the gRPC server. Defaults to secure HTTPS.</param>
    /// <param name="authToken">Optional authentication token for secure connections.</param>
    /// <param name="insecureMode">Set to true to use HTTP (insecure) connection.</param>
    /// <param name="certificatePath">Optional path to a PFX/PEM certificate file for trusting self-signed server certificates without OS-level installation.</param>
    /// <param name="certificatePassword">Optional password for the certificate file.</param>
    /// <param name="logger">Optional logger for diagnostics. When null, warnings are silently omitted.</param>
    public UiAutomationDriver(
        string address = "https://127.0.0.1:50051",
        string? authToken = null,
        bool insecureMode = false,
        string? certificatePath = null,
        string? certificatePassword = null,
        ILogger<UiAutomationDriver>? logger = null)
    {
        _insecureMode = insecureMode;
        _authToken = authToken;
        _logger = logger;

        if (_insecureMode)
        {
            _logger?.LogWarning(
                "INSECURE MODE: gRPC connection is NOT encrypted. " +
                "This mode should only be used for development/testing.");

            // For insecure connections, ensure address uses http
            if (address.StartsWith("https://"))
            {
                address = address.Replace("https://", "http://");
            }
        }
        else
        {
            // For secure connections, ensure address uses https
            if (address.StartsWith("http://") && !address.StartsWith("https://"))
            {
                address = address.Replace("http://", "https://");
            }
        }

        // Configure channel options
        var handler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true
        };

        if (!_insecureMode && !string.IsNullOrEmpty(certificatePath))
        {
            ConfigureCertificateTrust(handler, certificatePath, certificatePassword);
        }

        var channelOptions = new GrpcChannelOptions
        {
            HttpHandler = handler
        };

        _channel = GrpcChannel.ForAddress(address, channelOptions);

        // If auth token provided, create call invoker with auth interceptor
        if (!string.IsNullOrEmpty(_authToken))
        {
            _callInvoker = _channel.Intercept(metadata =>
            {
                metadata.Add("Authorization", $"Bearer {_authToken}");
                return metadata;
            });
            Client = new UiAutomationService.UiAutomationServiceClient(_callInvoker);
        }
        else
        {
            Client = new UiAutomationService.UiAutomationServiceClient(_channel);
        }
    }

    /// <summary>
    /// Configures the HTTP handler to trust a specific certificate for self-signed cert scenarios.
    /// Only the provided certificate is trusted — all other untrusted certs are rejected.
    /// </summary>
    private void ConfigureCertificateTrust(SocketsHttpHandler handler, string certificatePath, string? certificatePassword)
    {
        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException(
                $"Certificate file not found: '{certificatePath}'. " +
                "Provide a valid path to the server's PFX or PEM certificate.",
                certificatePath);
        }

        var trustedCert = new X509Certificate2(certificatePath, certificatePassword ?? "");
        _logger?.LogInformation("Loaded trusted certificate: Subject={Subject}, Thumbprint={Thumbprint}",
            trustedCert.Subject, trustedCert.Thumbprint);

        handler.SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, serverCert, _, sslPolicyErrors) =>
            {
                // If the cert is already trusted at OS level, accept it
                if (sslPolicyErrors == SslPolicyErrors.None)
                    return true;

                // Otherwise, validate that the server cert matches our pinned cert
                if (serverCert is null)
                    return false;

                bool thumbprintMatch = string.Equals(
                    serverCert.GetCertHashString(),
                    trustedCert.Thumbprint,
                    StringComparison.OrdinalIgnoreCase);

                if (!thumbprintMatch)
                {
                    _logger?.LogWarning(
                        "Certificate thumbprint mismatch. Expected={Expected}, Received={Received}",
                        trustedCert.Thumbprint, serverCert.GetCertHashString());
                }

                return thumbprintMatch;
            }
        };
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
    /// Sends keys to the active application, optionally focusing a target element first.
    /// </summary>
    /// <param name="keys">The keys to send.</param>
    /// <param name="wait">Whether to wait for processing.</param>
    /// <param name="runtimeId">
    /// Optional RuntimeId of an element to focus before sending. When supplied, the server focuses
    /// that element first so the keys land on a specific control instead of whatever currently has
    /// focus. When null/empty, keys go to the currently focused window (legacy behavior).
    /// </param>
    /// <returns>A tuple containing success status and message.</returns>
    public async Task<(bool Success, string Message)> SendKeysAsync(string keys, bool wait = true, string? runtimeId = null)
    {
        var request = new SendKeysRequest { Keys = keys, Wait = wait };
        if (!string.IsNullOrEmpty(runtimeId))
            request.RuntimeId = runtimeId;

        var response = await Client.SendKeysAsync(request);
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

    #region Cache Management

    /// <summary>
    /// Clears the server-side element cache.
    /// Call without arguments to clear all cached elements.
    /// Optionally specify a process ID or app name to clear only that application's cache.
    /// </summary>
    /// <param name="processId">Optional process ID to clear cache for a specific process.</param>
    /// <param name="appName">Optional app name to clear cache by name (like CloseApp).</param>
    /// <returns>A tuple containing success status and message.</returns>
    public async Task<(bool Success, string Message)> ClearCacheAsync(int processId = 0, string appName = "")
    {
        var request = new ClearCacheRequest();

        if (!string.IsNullOrEmpty(appName))
            request.AppName = appName;
        else if (processId > 0)
            request.ProcessId = processId;

        var response = await Client.ClearCacheAsync(request);
        return (response.Success, response.Message);
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

        await _channel.ShutdownAsync();
        _channel.Dispose();
    }

    #endregion
}
