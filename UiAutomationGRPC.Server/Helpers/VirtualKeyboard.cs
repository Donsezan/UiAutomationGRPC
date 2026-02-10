namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Provides virtual keyboard operations for UI automation.
    /// Wraps System.Windows.Forms.SendKeys functionality.
    /// </summary>
    public static class VirtualKeyboard
    {
        /// <summary>
        /// Sends keys and waits for them to be processed.
        /// </summary>
        public static void SendWait(string keys)
        {
            SendKeys.SendWait(keys);
        }

        /// <summary>
        /// Sends keys without waiting for them to be processed.
        /// </summary>
        public static void Send(string keys)
        {
            SendKeys.Send(keys);
        }

        /// <summary>
        /// Sends keys with optional wait behavior.
        /// </summary>
        public static void Send(string keys, bool wait)
        {
            if (wait)
                SendWait(keys);
            else
                Send(keys);
        }

        /// <summary>
        /// Sends a key with a delay for keyboard readiness (legacy method).
        /// </summary>
        public static void SendKey(string buttonKey)
        {
            Thread.Sleep(UsabilityTimeLimits.KeyboardReadiness);
            SendWait(buttonKey);
        }

        /// <summary>
        /// Sends a key multiple times with delays between each press.
        /// </summary>
        public static void SendKey(string buttonKey, int count)
        {
            for (var i = 0; i < count; i++)
            {
                SendKey(buttonKey);
            }
        }
    }
}
