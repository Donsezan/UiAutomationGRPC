using NUnit.Framework;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Tests
{
    [TestFixture]
    public class InteractionAccessGuardTests
    {
        // ═══════════════════════════════════════════════════════════════
        //  Known paths for testing — present on any Windows machine
        // ═══════════════════════════════════════════════════════════════

        private static readonly string NotepadPath = @"C:\Windows\System32\notepad.exe";
        private static readonly string CmdPath = @"C:\Windows\System32\cmd.exe";

        private static InteractionAccessGuard MakeGuard(
            List<WhiteListEntry>? whiteList = null,
            List<BlackListEntry>? blackList = null,
            bool restrictInteractions = true)
        {
            var config = new AppAccessConfig
            {
                WhiteList = whiteList ?? new(),
                BlackList = blackList ?? new(),
                RestrictInteractions = restrictInteractions
            };
            var validator = new AppAccessValidator(config);
            return new InteractionAccessGuard(validator, config);
        }

        /// <summary>
        /// Retrieves a PID for a known system process that is always running.
        /// </summary>
        private static int GetCurrentProcessId()
        {
            return System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        // ═══════════════════════════════════════════════════════════════
        //  1. Empty config → not restricting, everything allowed
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void EmptyConfig_IsNotActivelyRestricting()
        {
            var guard = MakeGuard();
            Assert.That(guard.IsActivelyRestricting, Is.False);
        }

        [Test]
        public void EmptyConfig_AllowsAnyProcess()
        {
            var guard = MakeGuard();
            var (allowed, _) = guard.IsAllowed(GetCurrentProcessId());
            Assert.That(allowed, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════
        //  2. RestrictInteractions = false → not restricting
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void RestrictInteractionsFalse_IsNotActivelyRestricting()
        {
            var guard = MakeGuard(
                whiteList: new() { new WhiteListEntry { Path = NotepadPath } },
                restrictInteractions: false);

            Assert.That(guard.IsActivelyRestricting, Is.False);
        }

        [Test]
        public void RestrictInteractionsFalse_AllowsAnyProcess()
        {
            var guard = MakeGuard(
                whiteList: new() { new WhiteListEntry { Path = NotepadPath } },
                restrictInteractions: false);

            var (allowed, _) = guard.IsAllowed(GetCurrentProcessId());
            Assert.That(allowed, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════
        //  3. WhiteList active → restricting, validates processes
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void WhiteListActive_IsActivelyRestricting()
        {
            var guard = MakeGuard(
                whiteList: new() { new WhiteListEntry { Path = NotepadPath } });

            Assert.That(guard.IsActivelyRestricting, Is.True);
        }

        [Test]
        public void WhiteListActive_BlocksNonWhitelistedProcess()
        {
            // Current test process should not match notepad.exe whitelist
            var guard = MakeGuard(
                whiteList: new() { new WhiteListEntry { Path = NotepadPath } });

            var (allowed, reason) = guard.IsAllowed(GetCurrentProcessId());
            Assert.That(allowed, Is.False);
            Assert.That(reason, Does.Contain("not in the whitelist"));
        }

        // ═══════════════════════════════════════════════════════════════
        //  4. BlackList active → restricting
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void BlackListActive_IsActivelyRestricting()
        {
            var guard = MakeGuard(
                blackList: new() { new BlackListEntry { Path = NotepadPath } });

            Assert.That(guard.IsActivelyRestricting, Is.True);
        }

        [Test]
        public void BlackListActive_AllowsNonBlacklistedProcess()
        {
            // Current test process should not match notepad blacklist
            var guard = MakeGuard(
                blackList: new() { new BlackListEntry { Path = NotepadPath } });

            var (allowed, _) = guard.IsAllowed(GetCurrentProcessId());
            Assert.That(allowed, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════
        //  5. Cache behavior — same PID returns same result
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void CacheHit_SecondCallReturnsSameResult()
        {
            var guard = MakeGuard(
                blackList: new() { new BlackListEntry { Path = NotepadPath } });

            int pid = GetCurrentProcessId();
            var result1 = guard.IsAllowed(pid);
            var result2 = guard.IsAllowed(pid);

            Assert.That(result2.Allowed, Is.EqualTo(result1.Allowed));
            Assert.That(result2.Reason, Is.EqualTo(result1.Reason));
        }

        // ═══════════════════════════════════════════════════════════════
        //  6. Dead process → graceful denial
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void DeadProcess_ReturnsGracefulDenial()
        {
            var guard = MakeGuard(
                whiteList: new() { new WhiteListEntry { Path = NotepadPath } });

            // Use an extremely high PID that almost certainly doesn't exist
            var (allowed, reason) = guard.IsAllowed(99999);
            Assert.That(allowed, Is.False);
            Assert.That(reason, Is.Not.Empty);
        }

        // ═══════════════════════════════════════════════════════════════
        //  7. Only whitelist entries with non-empty paths trigger restriction
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void WhiteListWithOnlyEmptyPaths_IsNotRestricting()
        {
            var guard = MakeGuard(
                whiteList: new() { new WhiteListEntry { Path = "" } });

            Assert.That(guard.IsActivelyRestricting, Is.False);
        }

        [Test]
        public void BlackListWithOnlyEmptyPaths_IsNotRestricting()
        {
            // Empty-path blacklist entries are for global arg restrictions,
            // not for interaction gating
            var guard = MakeGuard(
                blackList: new() { new BlackListEntry { Path = "", RestrictedArgs = new() { "/format" } } });

            Assert.That(guard.IsActivelyRestricting, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════
        //  8. Constructor validation
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void NullValidator_ThrowsArgumentNullException()
        {
            var config = new AppAccessConfig();
            Assert.Throws<ArgumentNullException>(() => new InteractionAccessGuard(null!, config));
        }

        [Test]
        public void NullConfig_ThrowsArgumentNullException()
        {
            var validator = new AppAccessValidator(new AppAccessConfig());
            Assert.Throws<ArgumentNullException>(() => new InteractionAccessGuard(validator, null!));
        }
    }
}
