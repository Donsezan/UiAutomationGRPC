using Grpc.Core;
using UiAutomation;
using System.Threading.Tasks;
using System;

namespace UiAutomationGRPC.Library
{
    public class UiAutomationDriver : IDisposable
    {
        private readonly Channel _channel;
        internal UiAutomationService.UiAutomationServiceClient Client { get; private set; }

        public UiAutomationDriver(string address = "127.0.0.1:50051")
        {
            _channel = new Channel(address, ChannelCredentials.Insecure);
            Client = new UiAutomationService.UiAutomationServiceClient(_channel);
        }

        //TODO: Update return type by providing Status and ProcessId
        public async Task<(bool Success, string Message, int ProcessId)> OpenApp(string appName, string arguments = "")
        {
            var response = await Client.OpenAppAsync(new AppRequest { AppName = appName, Arguments = arguments });
            return (response.Success, response.Message, response.ProcessId);
        }

        public (bool Success, string Message) CloseApp(string appName)
        {
             var response = Client.CloseApp(new AppRequest { AppName = appName });
             return (response.Success, response.Message);
        }

        public (bool Success, string Message) CloseAppByProcessId(int processId)
        {
            var response = Client.CloseAppByProcessId(new CloseAppByProcessIdRequest { ProcessId = processId });
            return (response.Success, response.Message);
        }

        public async Task<(bool Success, string Message, byte[] ImageData)> TakeElementScreenshot(string runtimeId)
        {
            var request = new ScreenshotRequest { Mode = ScreenshotMode.Element, RuntimeId = runtimeId };
            var response = await Client.TakeScreenshotAsync(request);
            return (response.Success, response.Message, response.ImageData.ToByteArray());
        }

        public async Task<(bool Success, string Message, byte[] ImageData)> TakeWindowScreenshot(string runtimeId = null, int? processId = 0)
        {
            var request = new ScreenshotRequest { Mode = ScreenshotMode.Window };
            if (!string.IsNullOrEmpty(runtimeId)) request.RuntimeId = runtimeId;
            if (processId.HasValue) request.ProcessId = processId.Value;

            var response = await Client.TakeScreenshotAsync(request);
            return (response.Success, response.Message, response.ImageData.ToByteArray());
        }
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
