using FlaUI.Core.AutomationElements;
using NUnit.Framework;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Tests
{
    /// <summary>
    /// Tests for <see cref="ElementCache"/> RuntimeId resolution.
    ///
    /// These exercise the real UIA3 stack against the always-present desktop root
    /// (<see cref="UiaRuntime.Desktop"/>), so they require an interactive Windows session.
    /// When UIA is unavailable (headless CI) each test is marked inconclusive rather than failing.
    ///
    /// The key regression here is the cache-disabled bug: with <c>Features:Cache:Enabled=false</c> the
    /// registry used to store nothing and every RuntimeId lookup failed. After the fix, locator
    /// metadata is stored and RuntimeIds resolve in both modes.
    /// </summary>
    [TestFixture]
    public class ElementCacheTests
    {
        private bool _previousEnabled;
        private AutomationElement? _root;

        [SetUp]
        public void SetUp()
        {
            _previousEnabled = ElementCache.Enabled;
            ElementCache.Clear();

            try
            {
                _root = UiaRuntime.Desktop;
            }
            catch
            {
                _root = null;
            }
        }

        [TearDown]
        public void TearDown()
        {
            ElementCache.Enabled = _previousEnabled;
            ElementCache.Clear();
        }

        private AutomationElement RequireRoot()
        {
            if (_root == null)
                Assert.Ignore("UI Automation root element is unavailable (no interactive desktop).");
            return _root!;
        }

        // ────────────────────────────── RuntimeId is always returned ──────────────────────────────

        [TestCase(true)]
        [TestCase(false)]
        public void CacheElement_ReturnsNonEmptyRuntimeId(bool cacheEnabled)
        {
            var root = RequireRoot();
            ElementCache.Enabled = cacheEnabled;

            string runtimeId = ElementCache.CacheElement(root);

            Assert.That(runtimeId, Is.Not.Null.And.Not.Empty);
        }

        // ────────────────────────────── Metadata is stored in BOTH modes ──────────────────────────────
        // (Before the fix, Count stayed 0 whenever caching was disabled.)

        [TestCase(true)]
        [TestCase(false)]
        public void CacheElement_StoresEntry_InBothModes(bool cacheEnabled)
        {
            var root = RequireRoot();
            ElementCache.Enabled = cacheEnabled;

            ElementCache.CacheElement(root);

            Assert.That(ElementCache.Count, Is.EqualTo(1));
        }

        // ────────────────────────────── RuntimeId resolves in BOTH modes ──────────────────────────────
        // This is the core regression: disabling the cache must NOT break RuntimeId addressing.

        [TestCase(true)]
        [TestCase(false)]
        public void TryGetLive_ResolvesCachedElement_InBothModes(bool cacheEnabled)
        {
            var root = RequireRoot();
            ElementCache.Enabled = cacheEnabled;
            string runtimeId = ElementCache.CacheElement(root);

            bool resolved = ElementCache.TryGetLive(runtimeId, out var live);

            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.True, "RuntimeId should resolve regardless of the cache flag");
                Assert.That(live, Is.Not.Null);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TryGetLive_ResolvesChildElement_InBothModes(bool cacheEnabled)
        {
            var root = RequireRoot();

            // A real descendant (top-level window) exercises the live-tree re-resolution path,
            // not just the root fallback. Skip if the session has no top-level windows.
            var walker = UiaRuntime.Automation.TreeWalkerFactory.GetControlViewWalker();
            var firstWindow = walker.GetFirstChild(root);
            if (firstWindow == null)
                Assert.Ignore("No top-level windows available to resolve in this session.");

            ElementCache.Enabled = cacheEnabled;
            string runtimeId = ElementCache.CacheElement(firstWindow!);

            bool resolved = ElementCache.TryGetLive(runtimeId, out var live);

            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.True, "A live child element should resolve in both modes");
                Assert.That(live, Is.Not.Null);
            });
        }

        // ────────────────────────────── Unknown / empty RuntimeId ──────────────────────────────

        [TestCase(true)]
        [TestCase(false)]
        public void TryGetLive_UnknownRuntimeId_ReturnsFalse(bool cacheEnabled)
        {
            RequireRoot();
            ElementCache.Enabled = cacheEnabled;

            bool resolved = ElementCache.TryGetLive("999999,888888,777777", out var live);

            Assert.Multiple(() =>
            {
                Assert.That(resolved, Is.False);
                Assert.That(live, Is.Null);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TryGetLive_EmptyRuntimeId_ReturnsFalse(bool cacheEnabled)
        {
            ElementCache.Enabled = cacheEnabled;

            Assert.That(ElementCache.TryGetLive("", out _), Is.False);
        }

        // ────────────────────────────── Clearing works in BOTH modes ──────────────────────────────

        [TestCase(true)]
        [TestCase(false)]
        public void ClearByProcess_RemovesEntries_InBothModes(bool cacheEnabled)
        {
            var root = RequireRoot();
            ElementCache.Enabled = cacheEnabled;
            ElementCache.CacheElement(root);
            int pid = root.Properties.ProcessId.ValueOrDefault;

            int removed = ElementCache.ClearByProcess(pid);

            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.GreaterThanOrEqualTo(1));
                Assert.That(ElementCache.Count, Is.EqualTo(0));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Clear_RemovesAllEntries_InBothModes(bool cacheEnabled)
        {
            var root = RequireRoot();
            ElementCache.Enabled = cacheEnabled;
            ElementCache.CacheElement(root);

            ElementCache.Clear();

            Assert.That(ElementCache.Count, Is.EqualTo(0));
        }

        // ────────────────────────────── StripExeExtension helper (UIA-free) ──────────────────────────────

        [TestCase("notepad.exe", "notepad")]
        [TestCase("notepad", "notepad")]
        [TestCase("My App.EXE", "My App")]
        [TestCase("calc.exe.exe", "calc.exe")]
        public void StripExeExtension_RemovesTrailingExe(string input, string expected)
        {
            Assert.That(ElementCache.StripExeExtension(input), Is.EqualTo(expected));
        }

        // ────────────────────────────── ClearByName (UIA-free) ──────────────────────────────
        // Entries are injected via the metadata-overload so no real AutomationElement is needed.
        // ClearByName resolves name → PIDs via Process.GetProcessesByName and then filters the
        // cache by PID — the element reference itself is never touched.

        [Test]
        public void ClearByName_NullOrEmpty_ReturnsZeroWithoutThrowing()
        {
            InsertFakeEntry("id-x", 5000);

            Assert.Multiple(() =>
            {
                Assert.That(ElementCache.ClearByName(null!), Is.EqualTo(0));
                Assert.That(ElementCache.ClearByName(""), Is.EqualTo(0));
                Assert.That(ElementCache.Count, Is.EqualTo(1), "Unrelated entry must not be removed");
            });
        }

        [Test]
        public void ClearByName_UnknownProcessName_RemovesNothing()
        {
            InsertFakeEntry("id-y", 5001);

            int removed = ElementCache.ClearByName("__no_such_process_xyz__");

            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.EqualTo(0));
                Assert.That(ElementCache.Count, Is.EqualTo(1));
            });
        }

        [Test]
        public void ClearByName_WithPlainName_ClearsMatchingEntries()
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            InsertFakeEntry("id-proc-plain", proc.Id);
            InsertFakeEntry("id-unrelated", proc.Id + 99999);

            int removed = ElementCache.ClearByName(proc.ProcessName);

            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.GreaterThanOrEqualTo(1));
                Assert.That(ElementCache.TryGetLive("id-proc-plain", out _), Is.False,
                    "Entry for the current process must be evicted");
            });
        }

        [Test]
        public void ClearByName_WithExeSuffix_MatchesSameProcessAsWithout()
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            InsertFakeEntry("id-proc-exe", proc.Id);

            // .exe suffix must be stripped before the GetProcessesByName call
            int removed = ElementCache.ClearByName(proc.ProcessName + ".exe");

            Assert.That(removed, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void ClearByName_ExeSuffixAndPlainName_ProduceSameResult()
        {
            var proc = System.Diagnostics.Process.GetCurrentProcess();

            InsertFakeEntry("id-a", proc.Id);
            int withSuffix = ElementCache.ClearByName(proc.ProcessName + ".exe");

            ElementCache.Clear();
            InsertFakeEntry("id-b", proc.Id);
            int withoutSuffix = ElementCache.ClearByName(proc.ProcessName);

            Assert.That(withSuffix, Is.EqualTo(withoutSuffix),
                ".exe suffix must produce the same removal count as the plain name");
        }

        // ────────────────────────────── Helper ──────────────────────────────

        private static void InsertFakeEntry(string runtimeId, int processId)
        {
            // null element is safe: ClearByProcess/ClearByName only inspect ProcessId.
            ElementCache.CacheElement(null!, runtimeId, "", "fake", "", "", processId);
        }
    }
}
