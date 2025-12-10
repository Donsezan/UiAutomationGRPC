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
        internal UiAutomationService.UiAutomationServiceClient Client { get; private set; }

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
        public (bool Success, string Message) CloseApp(string appName)
        {
             var response = Client.CloseApp(new AppRequest { AppName = appName });
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
