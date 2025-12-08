
using Grpc.Core.Logging;
using UiAutomation;

namespace UiAutomationGRPC.Library.Helpers
{
    public static class KeyboardHelper
    {
        private static UiAutomationService.UiAutomationServiceClient _client;

        public static void Init(UiAutomationService.UiAutomationServiceClient client)
        {
            _client = client;
        }

        public static void SendKey(string buttonKey, int count)
        {

            for (var send = 0; send < count; send++)
            {
                SendKey(buttonKey);
            }
        }

        public static void SendKey(string buttonKey)
        {
            System.Threading.Thread.Sleep(UsabilityTimeLimits.KeyboardReadiness);
            SendKeyInternal(buttonKey);
        }

        private static void SendKeyInternal(string buttonKey)
        {
            if (_client != null)
            {
                // Synchronous call for now, as SendSendKeys is void in helper usage but async in gRPC
                // Using .GetAwaiter().GetResult() to keep method signature if needed, or fire and forget. 
                // Given SendKey in tests usually expects action completion, we block.
                try 
                {
                    _client.SendKeys(new UiAutomation.SendKeysRequest { Keys = buttonKey, Wait = true });
                    Logger.WriteLog(buttonKey + " sent via gRPC");
                }
                catch (System.Exception ex)
                {
                    Logger.WriteLog($"Failed to send key {buttonKey}: {ex.Message}");
                }
            }
            else
            {
                Logger.WriteLog(buttonKey + " sent (simulation only, no client)");
            }
        }

    }
}
