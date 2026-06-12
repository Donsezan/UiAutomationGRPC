using NUnit.Framework;
using System.Diagnostics;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Tests
{
    [TestFixture]
    public class ElementCacheSweepTests
    {
        [SetUp]
        public void SetUp()
        {
            ElementCache.Clear();
            StructureSnapshotStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ElementCache.Clear();
            StructureSnapshotStore.Clear();
        }

        private static int DeadPid()
        {
            // A real PID that is guaranteed dead: spawn a no-op process and wait for it to exit.
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit",
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            p.WaitForExit();
            return p.Id;
        }

        [Test]
        public void SweepDeadProcesses_RemovesEntriesOfExitedProcess()
        {
            int deadPid = DeadPid();
            int livePid = Process.GetCurrentProcess().Id;

            ElementCache.CacheElement(null!, "dead-1", "a", "n", "c", "t", deadPid);
            ElementCache.CacheElement(null!, "dead-2", "a", "n", "c", "t", deadPid);
            ElementCache.CacheElement(null!, "live-1", "a", "n", "c", "t", livePid);

            int removed = ElementCache.SweepDeadProcesses();

            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.EqualTo(2));
                Assert.That(ElementCache.Count, Is.EqualTo(1), "only the live-pid entry survives");
            });
        }

        [Test]
        public void SweepDeadProcesses_AlsoEvictsStructureSnapshots()
        {
            int deadPid = DeadPid();
            StructureSnapshotStore.Set("w-dead", deadPid, new AppNode { UniqId = "w-dead" });
            ElementCache.CacheElement(null!, "dead-1", "a", "n", "c", "t", deadPid);

            ElementCache.SweepDeadProcesses();

            Assert.That(StructureSnapshotStore.Get("w-dead"), Is.Null);
        }

        [Test]
        public void SweepDeadProcesses_EmptyCache_NoOp()
        {
            Assert.That(ElementCache.SweepDeadProcesses(), Is.EqualTo(0));
        }

        [Test]
        public void SweepDeadProcesses_KeepsLiveProcessEntries()
        {
            int livePid = Process.GetCurrentProcess().Id;
            ElementCache.CacheElement(null!, "live-1", "a", "n", "c", "t", livePid);

            int removed = ElementCache.SweepDeadProcesses();

            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.EqualTo(0));
                Assert.That(ElementCache.Count, Is.EqualTo(1));
            });
        }
    }
}
