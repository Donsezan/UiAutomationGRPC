using Grpc.Core.Logging;

namespace CoreTest.Helpers
{
    public static class KeyboardHelper
    {
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
            //TODO : Implement GRPC action key sending
            Logger.WriteLog(buttonKey + " sent");
        }

    }
}
