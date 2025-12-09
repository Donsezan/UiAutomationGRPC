using Grpc.Core;
using UiAutomation;
using System.Threading.Tasks;
using System;

namespace UiAutomationGRPC.Library
{
    public class UiAutomationDriver : IDisposable
    {
        private readonly Channel _channel;
        public UiAutomationService.UiAutomationServiceClient Client { get; private set; }

        public UiAutomationDriver(string address = "127.0.0.1:50051")
        {
            _channel = new Channel(address, ChannelCredentials.Insecure);
            Client = new UiAutomationService.UiAutomationServiceClient(_channel);
        }

        public async Task<int> OpenApp(string appName, string arguments = "")
        {
            var response = await Client.OpenAppAsync(new AppRequest { AppName = appName, Arguments = arguments });
            if (!response.Success)
            {
                throw new Exception($"Failed to open app {appName}: {response.Message}");
            }
            return response.ProcessId;
        }

        public void CloseApp(string appName)
        {
             var response = Client.CloseApp(new AppRequest { AppName = appName });
             if (!response.Success)
             {
                 // Log or throw? For now just log or ignore
             }
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
