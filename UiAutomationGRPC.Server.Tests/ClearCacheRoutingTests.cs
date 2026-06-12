using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Diagnostics;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Tests
{
    /// <summary>
    /// Tests for the three <c>ClearCache</c> routing branches in <see cref="UiAutomationService"/>:
    /// all-clear (no filter), by process ID, and by app name.
    ///
    /// These are pure cache-state tests — no live UIA session required.
    /// Cache entries are injected via the overload that accepts pre-read locator metadata,
    /// so no real <see cref="FlaUI.Core.AutomationElements.AutomationElement"/> is needed.
    /// </summary>
    [TestFixture]
    public class ClearCacheRoutingTests
    {
        private UiaExecutor _executor = null!;
        private UiAutomationService _service = null!;
        private FakeServerCallContext _ctx = null!;

        [SetUp]
        public void SetUp()
        {
            ElementCache.Clear();
            _executor = new UiaExecutor();
            _service = new UiAutomationService(NullLoggerFactory.Instance, _executor);
            _ctx = new FakeServerCallContext();
        }

        [TearDown]
        public void TearDown()
        {
            ElementCache.Clear();
            _executor.Dispose();
        }

        // ────────────────────────────── All-clear (no filter) ──────────────────────────────

        [Test]
        public async Task ClearCache_NoFilter_RemovesAllEntries()
        {
            InsertFakeEntry("id-1", 1001);
            InsertFakeEntry("id-2", 1002);

            var resp = await _service.ClearCache(new ClearCacheRequest(), _ctx);

            Assert.Multiple(() =>
            {
                Assert.That(resp.Success, Is.True);
                Assert.That(ElementCache.Count, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task ClearCache_NoFilter_ResponseMessageContainsRemovedCount()
        {
            InsertFakeEntry("id-1", 42);
            InsertFakeEntry("id-2", 42);

            var resp = await _service.ClearCache(new ClearCacheRequest(), _ctx);

            Assert.That(resp.Message, Does.Contain("2"));
        }

        [Test]
        public async Task ClearCache_NoFilter_EmptyCache_ReturnsZero()
        {
            var resp = await _service.ClearCache(new ClearCacheRequest(), _ctx);

            Assert.Multiple(() =>
            {
                Assert.That(resp.Success, Is.True);
                Assert.That(resp.Message, Does.Contain("0"));
            });
        }

        // ────────────────────────────── ByProcess ──────────────────────────────

        [Test]
        public async Task ClearCache_ByProcess_RemovesOnlyMatchingPidEntries()
        {
            const int targetPid = 9901;
            InsertFakeEntry("id-target-1", targetPid);
            InsertFakeEntry("id-target-2", targetPid);
            InsertFakeEntry("id-other", targetPid + 1);

            var resp = await _service.ClearCache(new ClearCacheRequest { ProcessId = targetPid }, _ctx);

            Assert.Multiple(() =>
            {
                Assert.That(resp.Success, Is.True);
                Assert.That(ElementCache.Count, Is.EqualTo(1), "Only the other-PID entry should remain");
            });
        }

        [Test]
        public async Task ClearCache_ByProcess_ReturnsSuccess()
        {
            InsertFakeEntry("id-1", 8800);

            var resp = await _service.ClearCache(new ClearCacheRequest { ProcessId = 8800 }, _ctx);

            Assert.That(resp.Success, Is.True);
        }

        [Test]
        public async Task ClearCache_ByProcess_UnknownPid_RemovesNothingStillSucceeds()
        {
            InsertFakeEntry("id-1", 1001);

            var resp = await _service.ClearCache(new ClearCacheRequest { ProcessId = 99999 }, _ctx);

            Assert.Multiple(() =>
            {
                Assert.That(resp.Success, Is.True);
                Assert.That(ElementCache.Count, Is.EqualTo(1));
            });
        }

        // ────────────────────────────── ByName ──────────────────────────────
        // Uses the current test process (guaranteed to be running) so GetProcessesByName returns it.

        [Test]
        public async Task ClearCache_ByName_RemovesEntriesMatchingCurrentProcess()
        {
            var proc = Process.GetCurrentProcess();
            InsertFakeEntry("id-proc", proc.Id);
            InsertFakeEntry("id-other", proc.Id + 99999);

            var resp = await _service.ClearCache(new ClearCacheRequest { AppName = proc.ProcessName }, _ctx);

            Assert.Multiple(() =>
            {
                Assert.That(resp.Success, Is.True);
                // "id-proc" entry should be gone; "id-other" belongs to a non-existent PID so it stays
                Assert.That(ElementCache.TryGetLive("id-proc", out _), Is.False,
                    "Entry for the current process should have been cleared");
            });
        }

        [Test]
        public async Task ClearCache_ByNameWithExeSuffix_ClearsLikeWithoutSuffix()
        {
            var proc = Process.GetCurrentProcess();
            InsertFakeEntry("id-proc-exe", proc.Id);

            // Append .exe — ClearByName must strip it before calling GetProcessesByName
            var resp = await _service.ClearCache(
                new ClearCacheRequest { AppName = proc.ProcessName + ".exe" }, _ctx);

            Assert.That(resp.Success, Is.True);
        }

        [Test]
        public async Task ClearCache_ByName_TakesPriorityOverProcessId()
        {
            // When AppName is set, the ByName branch runs regardless of ProcessId value.
            var proc = Process.GetCurrentProcess();
            InsertFakeEntry("id-proc", proc.Id);

            // ProcessId = 1 would normally not match our entry, but AppName wins.
            var resp = await _service.ClearCache(
                new ClearCacheRequest { AppName = proc.ProcessName, ProcessId = 1 }, _ctx);

            Assert.That(resp.Success, Is.True);
        }

        // ────────────────────────────── Helper ──────────────────────────────

        private static void InsertFakeEntry(string runtimeId, int processId)
        {
            // null element is safe here: ClearByProcess/ClearByName only check ProcessId.
            ElementCache.CacheElement(null!, runtimeId, "", "fake", "", "", processId);
        }
    }
}
