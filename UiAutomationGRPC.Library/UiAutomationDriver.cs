using Grpc.Core;
using UiAutomation;
using System.Threading.Tasks;
using System;

namespace UiAutomationGRPC.Library
{
    /// <summary>
    /// Driver for interacting with the UI Automation gRPC service.
    /// </summary>
    public class UiAutomationDriver : IDisposable
    {
        private readonly Channel _channel;
        
        /// <summary>
        /// Internal gRPC client.
        /// </summary>
        public UiAutomationService.UiAutomationServiceClient Client { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UiAutomationDriver"/> class.
        /// </summary>
        /// <param name="address">The address of the gRPC server.</param>
        public UiAutomationDriver(string address = "127.0.0.1:50051")
        {
            _channel = new Channel(address, ChannelCredentials.Insecure);
            Client = new UiAutomationService.UiAutomationServiceClient(_channel);
        }

        /// <summary>
        /// Opens an application.
        /// </summary>
        /// <param name="appName">The name or path of the application.</param>
        /// <param name="arguments">The arguments to pass to the application.</param>
        /// <returns>A tuple containing success status, message, and process ID.</returns>
        public async Task<(bool Success, string Message, int ProcessId)> OpenApp(string appName, string arguments = "")
        {
            var response = await Client.OpenAppAsync(new AppRequest { AppName = appName, Arguments = arguments });
            return (response.Success, response.Message, response.ProcessId);
        }

        /// <summary>
        /// Closes an application by name.
        /// </summary>
        /// <param name="appName">The name of the application.</param>
        /// <returns>A tuple containing success status and message.</returns>
        public (bool Success, string Message) CloseApp(string appName, bool force = false)
        {
             var response = Client.CloseApp(new AppRequest { AppName = appName, Force = force });
             return (response.Success, response.Message);
        }

        /// <summary>
        /// Closes an application by process ID.
        /// </summary>
        /// <param name="processId">The process ID of the application.</param>
        /// <returns>A tuple containing success status and message.</returns>
        public (bool Success, string Message) CloseAppByProcessId(int processId)
        {
            var response = Client.CloseAppByProcessId(new CloseAppByProcessIdRequest { ProcessId = processId });
            return (response.Success, response.Message);
        }

        /// <summary>
        /// Takes a screenshot of a specific element.
        /// </summary>
        /// <param name="runtimeId">The runtime ID of the element.</param>
        /// <returns>A tuple containing success status, message, and image data.</returns>
        public async Task<(bool Success, string Message, byte[] ImageData)> TakeElementScreenshot(string runtimeId)
        {
            var request = new ScreenshotRequest { Mode = ScreenshotMode.Element, RuntimeId = runtimeId };
            var response = await Client.TakeScreenshotAsync(request);
            return (response.Success, response.Message, response.ImageData.ToByteArray());
        }

        /// <summary>
        /// Takes a screenshot of the window or screen.
        /// </summary>
        /// <param name="runtimeId">Optional runtime ID to highlight.</param>
        /// <param name="processId">Optional process ID to target a specific window.</param>
        /// <returns>A tuple containing success status, message, and image data.</returns>
        public async Task<(bool Success, string Message, byte[] ImageData)> TakeWindowScreenshot(string runtimeId = null, int? processId = 0)
        {
            var request = new ScreenshotRequest { Mode = ScreenshotMode.Window };
            if (!string.IsNullOrEmpty(runtimeId)) request.RuntimeId = runtimeId;
            if (processId.HasValue) request.ProcessId = processId.Value;

            var response = await Client.TakeScreenshotAsync(request);
            return (response.Success, response.Message, response.ImageData.ToByteArray());
        }

        /// <summary>
        /// Gets children of an element.
        /// </summary>
        /// <param name="runtimeId">The runtime ID of the parent element.</param>
        /// <returns>A tuple containing success status, message, and list of elements.</returns>
        public async Task<(bool Success, string Message, System.Collections.Generic.List<ElementResponse> Elements)> GetChildren(string runtimeId = "")
        {
            var request = new GetChildrenRequest { RuntimeId = runtimeId ?? "" };
            var response = await Client.GetChildrenAsync(request);
            var list = new System.Collections.Generic.List<ElementResponse>();
            if (response.Elements != null) list.AddRange(response.Elements);
            return (response.Success, response.Message, list);
        }

        /// <summary>
        /// Finds an element based on a condition.
        /// </summary>
        /// <param name="startRuntimeId">The runtime ID to start searching from (empty for Desktop).</param>
        /// <param name="condition">The condition to match.</param>
        /// <param name="scope">The scope of the search.</param>
        /// <returns>A tuple containing success status, message, and the found element.</returns>
        public async Task<(bool Success, string Message, ElementResponse Element)> FindElement(string startRuntimeId, Condition condition, TreeScope scope)
        {
            try
            {
                var request = new FindElementRequest { StartRuntimeId = startRuntimeId ?? "", Condition = condition, Scope = scope };
                var response = await Client.FindElementAsync(request);
                return (true, "Found", response);
            }
            catch (RpcException ex)
            {
                return (false, ex.Status.Detail ?? ex.Message, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        /// <summary>
        /// Performs an action on an element.
        /// </summary>
        /// <param name="runtimeId">The runtime ID of the element.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="arguments">Optional arguments for the action.</param>
        /// <returns>A tuple containing success status and message.</returns>
        public async Task<(bool Success, string Message)> PerformAction(string runtimeId, ActionType action, System.Collections.Generic.IEnumerable<string> arguments = null)
        {
            var request = new PerformActionRequest { RuntimeId = runtimeId, Action = action };
            if (arguments != null) request.Arguments.AddRange(arguments);
            var response = await Client.PerformActionAsync(request);
            return (response.Success, response.Message);
        }

        /// <summary>
        /// Gets a property of an element.
        /// </summary>
        /// <param name="runtimeId">The runtime ID of the element.</param>
        /// <param name="propertyName">The name of the property to retrieve.</param>
        /// <returns>A tuple containing success status, message, and the property value.</returns>
        public async Task<(bool Success, string Message, string Value)> GetProperty(string runtimeId, string propertyName)
        {
            var request = new GetPropertyRequest { RuntimeId = runtimeId, PropertyName = propertyName };
            var response = await Client.GetPropertyAsync(request);
            return (response.Success, response.Message, response.Value);
        }

        /// <summary>
        /// Sends keys to the active application.
        /// </summary>
        /// <param name="keys">The keys to send.</param>
        /// <param name="wait">Whether to wait for the keys to be processed.</param>
        /// <returns>A tuple containing success status and message.</returns>
        public async Task<(bool Success, string Message)> SendKeys(string keys, bool wait = true)
        {
            var request = new SendKeysRequest { Keys = keys, Wait = wait };
            var response = await Client.SendKeysAsync(request);
            return (response.Success, response.Message);
        }

        /// <summary>
        /// Reflects on automation properties, patterns, or control types.
        /// </summary>
        /// <param name="target">The reflection target.</param>
        /// <param name="runtimeId">Optional runtime ID for element-specific reflection.</param>
        /// <returns>A tuple containing success status, message, and list of reflection entries.</returns>
        public async Task<(bool Success, string Message, System.Collections.Generic.List<ReflectionEntry> Entries)> Reflect(ReflectionTarget target, string runtimeId = "")
        {
            var request = new ReflectionRequest { Target = target, RuntimeId = runtimeId ?? "" };
            var response = await Client.ReflectAsync(request);
            var list = new System.Collections.Generic.List<ReflectionEntry>();
            if (response.Entries != null) list.AddRange(response.Entries);
            return (response.Success, response.Message, list);
        }

        /// <summary>
        /// Disposes the driver and shuts down the channel.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _channel.ShutdownAsync().Wait();
            }
            catch { }
        }
    }
}
