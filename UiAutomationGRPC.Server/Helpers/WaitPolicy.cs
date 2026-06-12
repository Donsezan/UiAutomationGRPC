namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Normalizes the client-supplied timing of <c>WaitForElement</c>. Zero/negative values fall
    /// back to defaults; both are clamped so a single client cannot park the request pipeline on
    /// an effectively-infinite wait or hammer the UIA worker with a sub-50&#160;ms poll loop.
    /// </summary>
    public static class WaitPolicy
    {
        public const int DefaultTimeoutMs = 10_000;
        public const int MaxTimeoutMs = 120_000;
        public const int DefaultPollIntervalMs = 250;
        public const int MinPollIntervalMs = 50;
        public const int MaxPollIntervalMs = 5_000;

        public static (int TimeoutMs, int PollIntervalMs) Normalize(int timeoutMs, int pollIntervalMs)
        {
            int timeout = timeoutMs <= 0 ? DefaultTimeoutMs : Math.Min(timeoutMs, MaxTimeoutMs);
            int poll = pollIntervalMs <= 0
                ? DefaultPollIntervalMs
                : Math.Clamp(pollIntervalMs, MinPollIntervalMs, MaxPollIntervalMs);
            return (timeout, poll);
        }
    }
}
