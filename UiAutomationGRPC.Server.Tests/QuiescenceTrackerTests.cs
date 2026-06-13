using NUnit.Framework;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Tests
{
    /// <summary>
    /// Pure-math tests for the settle detector core. Tick values are arbitrary units with
    /// ticksPerMs = 10 for readable arithmetic (1 ms == 10 ticks).
    /// </summary>
    [TestFixture]
    public class QuiescenceTrackerTests
    {
        private const double TicksPerMs = 10.0;

        [Test]
        public void IsQuiet_FalseImmediatelyAfterConstruction()
        {
            var tracker = new QuiescenceTracker(nowTicks: 1000);
            Assert.That(tracker.IsQuiet(nowTicks: 1000, quietMs: 100, TicksPerMs), Is.False);
        }

        [Test]
        public void IsQuiet_TrueAfterQuietPeriodElapses()
        {
            var tracker = new QuiescenceTracker(nowTicks: 1000);
            // 100 ms quiet = 1000 ticks; at t=2000 exactly the period has elapsed.
            Assert.That(tracker.IsQuiet(nowTicks: 2000, quietMs: 100, TicksPerMs), Is.True);
        }

        [Test]
        public void IsQuiet_FalseJustBeforeQuietPeriodElapses()
        {
            var tracker = new QuiescenceTracker(nowTicks: 1000);
            Assert.That(tracker.IsQuiet(nowTicks: 1999, quietMs: 100, TicksPerMs), Is.False);
        }

        [Test]
        public void RecordChange_ResetsTheQuietWindow()
        {
            var tracker = new QuiescenceTracker(nowTicks: 1000);
            tracker.RecordChange(nowTicks: 1900); // event arrives 90 ms in

            Assert.Multiple(() =>
            {
                Assert.That(tracker.IsQuiet(nowTicks: 2000, quietMs: 100, TicksPerMs), Is.False,
                    "quiet window must restart from the last change");
                Assert.That(tracker.IsQuiet(nowTicks: 2900, quietMs: 100, TicksPerMs), Is.True,
                    "quiet again 100 ms after the last change");
            });
        }

        [Test]
        public void RecordChange_RepeatedEvents_KeepItNotQuiet()
        {
            var tracker = new QuiescenceTracker(nowTicks: 0);
            for (long t = 500; t <= 5000; t += 500) // an event every 50 ms — busier than quietMs
                tracker.RecordChange(t);

            Assert.That(tracker.IsQuiet(nowTicks: 5400, quietMs: 100, TicksPerMs), Is.False);
        }
    }
}
