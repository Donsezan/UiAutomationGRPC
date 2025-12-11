
using Grpc.Core.Logging;
using UiAutomation;

namespace UiAutomationGRPC.Library.Helpers
{
    /// <summary>
    /// Helper for simulating keyboard input via gRPC.
    /// </summary>
    public static class KeyboardHelper
    {
        private static UiAutomationService.UiAutomationServiceClient _client;

        /// <summary>
        /// Initializes the keyboard helper with a driver.
        /// </summary>
        /// <param name="driver">The driver instance.</param>
        public static void Init(UiAutomationDriver driver)
        {
            _client = driver.Client;
        }

        /// <summary>
        /// Sends a key multiple times.
        /// </summary>
        /// <param name="buttonKey">The key to send.</param>
        /// <param name="count">Number of times to send.</param>
        public static void SendKey(string buttonKey, int count)
        {
            for (var send = 0; send < count; send++)
            {
                SendKey(buttonKey);
            }
        }

        /// <summary>
        /// Sends a key.
        /// </summary>
        /// <param name="buttonKey">The key to send.</param>
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
