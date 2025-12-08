using System.Windows.Forms;

namespace UiAutomationGRPC.Server.Helpers
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
            SendKeys.SendWait(buttonKey);
        }
    }
}
