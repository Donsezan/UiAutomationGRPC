using NUnit.Framework;
using System.Diagnostics;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Tests
{
    [TestFixture]
    public class UwpPidResolverTests
    {
        private static DateTime? NoStartTime(int _) => null;

        [Test]
        public void PickNewWindowPid_NoCandidates_ReturnsZero()
        {
            var before = new HashSet<int> { 100, 200 };
            Assert.That(UwpPidResolver.PickNewWindowPid(before, new[] { 100, 200 }, NoStartTime), Is.EqualTo(0));
        }

        [Test]
        public void PickNewWindowPid_EmptyAfter_ReturnsZero()
        {
            Assert.That(UwpPidResolver.PickNewWindowPid(new HashSet<int>(), Array.Empty<int>(), NoStartTime), Is.EqualTo(0));
        }

        [Test]
        public void PickNewWindowPid_SingleNewPid_ReturnsIt()
        {
            var before = new HashSet<int> { 100 };
            Assert.That(UwpPidResolver.PickNewWindowPid(before, new[] { 100, 555 }, NoStartTime), Is.EqualTo(555));
        }

        [Test]
        public void PickNewWindowPid_MultipleNew_PicksMostRecentlyStarted()
        {
            var before = new HashSet<int> { 100 };
            var now = DateTime.Now;
            DateTime? Lookup(int pid) => pid switch
            {
                555 => now.AddSeconds(-30),
                666 => now.AddSeconds(-1), // newest — the app we just launched
                777 => now.AddSeconds(-60),
                _ => null
            };

            Assert.That(UwpPidResolver.PickNewWindowPid(before, new[] { 100, 555, 666, 777 }, Lookup), Is.EqualTo(666));
        }

        [Test]
        public void PickNewWindowPid_AllStartTimesUnknown_FallsBackToFirstCandidate()
        {
            var before = new HashSet<int>();
            Assert.That(UwpPidResolver.PickNewWindowPid(before, new[] { 11, 22 }, NoStartTime), Is.EqualTo(11));
        }

        [Test]
        public void PickNewWindowPid_IgnoresNonPositivePids()
        {
            var before = new HashSet<int>();
            Assert.That(UwpPidResolver.PickNewWindowPid(before, new[] { 0, -5 }, NoStartTime), Is.EqualTo(0));
        }

        [Test]
        public void VisibleTopLevelWindowPids_ReturnsNonEmptySet()
        {
            // Any interactive session has at least the shell's windows.
            var pids = UwpPidResolver.VisibleTopLevelWindowPids();
            Assert.That(pids, Is.Not.Empty);
        }

        [Test]
        public void ResolveLaunchedPid_SurvivingProcess_ReturnsOwnPidUnresolved()
        {
            // A process that outlives the grace window must keep its own PID (Win32 fast path).
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping -n 4 127.0.0.1 >nul", // ~3 s lifetime
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Assert.That(process, Is.Not.Null);

            try
            {
                var before = UwpPidResolver.VisibleTopLevelWindowPids();
                var (pid, resolved, launcherExited) = UwpPidResolver.ResolveLaunchedPid(process, before);

                Assert.Multiple(() =>
                {
                    Assert.That(pid, Is.EqualTo(process!.Id));
                    Assert.That(resolved, Is.False);
                    Assert.That(launcherExited, Is.False);
                });
            }
            finally
            {
                try { process!.Kill(); } catch { /* already exited */ }
            }
        }

        [Test]
        public void ResolveLaunchedPid_NullProcess_ReturnsZero()
        {
            var (pid, resolved, launcherExited) = UwpPidResolver.ResolveLaunchedPid(null, new HashSet<int>());
            Assert.Multiple(() =>
            {
                Assert.That(pid, Is.EqualTo(0));
                Assert.That(resolved, Is.False);
                Assert.That(launcherExited, Is.False);
            });
        }
    }
}
