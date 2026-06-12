using System.Diagnostics;
using System.Windows.Automation;
using Trace = System.Diagnostics.Trace;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Tracks UI-change events to decide when the UI has "settled" (no changes for a quiet
    /// period). Pure timestamp math lives here so it can be unit tested; the UIA event plumbing
    /// is in <see cref="UiSettler"/>.
    /// </summary>
    public sealed class QuiescenceTracker
    {
        private long _lastChangeTicks;

        public QuiescenceTracker(long nowTicks) => _lastChangeTicks = nowTicks;

        public void RecordChange(long nowTicks) => Interlocked.Exchange(ref _lastChangeTicks, nowTicks);

        /// <summary>True when at least <paramref name="quietMs"/> have passed since the last recorded change.</summary>
        public bool IsQuiet(long nowTicks, int quietMs, double ticksPerMs) =>
            (nowTicks - Interlocked.Read(ref _lastChangeTicks)) / ticksPerMs >= quietMs;
    }

    /// <summary>
    /// Waits for a window's UI to settle after an action before the refreshed tree is read.
    ///
    /// Subscribes to structure-changed and Name/Value property-changed events under the window
    /// and returns once no event has arrived for <c>quietMs</c> — or after <c>maxMs</c> regardless,
    /// which is essential for continuously-updating apps (tickers, grids, streaming data) where a
    /// quiet period may never come. Falls back to a fixed delay when event subscription fails
    /// (some legacy providers reject subtree subscriptions).
    /// </summary>
    public static class UiSettler
    {
        public const int FallbackSettleMs = 200;
        private const int PollSliceMs = 25;

        public static void WaitForQuiet(AutomationElement root, int quietMs, int maxMs)
        {
            if (quietMs <= 0) return;
            if (maxMs < quietMs) maxMs = quietMs;

            double ticksPerMs = Stopwatch.Frequency / 1000.0;
            var tracker = new QuiescenceTracker(Stopwatch.GetTimestamp());

            StructureChangedEventHandler structureHandler = (_, _) => tracker.RecordChange(Stopwatch.GetTimestamp());
            AutomationPropertyChangedEventHandler propertyHandler = (_, _) => tracker.RecordChange(Stopwatch.GetTimestamp());

            bool structureSubscribed = false, propertySubscribed = false;
            try
            {
                try
                {
                    Automation.AddStructureChangedEventHandler(root, TreeScope.Subtree, structureHandler);
                    structureSubscribed = true;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[UiSettler] Structure event subscription failed: {ex.Message}");
                }

                try
                {
                    Automation.AddAutomationPropertyChangedEventHandler(
                        root, TreeScope.Subtree, propertyHandler,
                        AutomationElement.NameProperty, ValuePattern.ValueProperty);
                    propertySubscribed = true;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[UiSettler] Property event subscription failed: {ex.Message}");
                }

                if (!structureSubscribed && !propertySubscribed)
                {
                    // No event feed at all — keep the legacy fixed delay.
                    Thread.Sleep(FallbackSettleMs);
                    return;
                }

                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < maxMs)
                {
                    if (tracker.IsQuiet(Stopwatch.GetTimestamp(), quietMs, ticksPerMs))
                        return;
                    Thread.Sleep(PollSliceMs);
                }
                // maxMs reached with the UI still chattering — proceed anyway (dynamic app).
            }
            finally
            {
                if (structureSubscribed)
                {
                    try { Automation.RemoveStructureChangedEventHandler(root, structureHandler); }
                    catch (Exception ex) { Trace.WriteLine($"[UiSettler] Structure event unsubscribe failed: {ex.Message}"); }
                }
                if (propertySubscribed)
                {
                    try { Automation.RemoveAutomationPropertyChangedEventHandler(root, propertyHandler); }
                    catch (Exception ex) { Trace.WriteLine($"[UiSettler] Property event unsubscribe failed: {ex.Message}"); }
                }
            }
        }
    }
}
