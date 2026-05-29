using NUnit.Framework;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Tests
{
    [TestFixture]
    public class AppAccessValidatorTests
    {
        // ═══════════════════════════════════════════════════════════════
        //  Helper: known-to-exist paths on any Windows machine
        // ═══════════════════════════════════════════════════════════════

        private static readonly string NotepadPath = @"C:\Windows\System32\notepad.exe";
        private static readonly string CmdPath = @"C:\Windows\System32\cmd.exe";
        private static readonly string IpconfigPath = @"C:\Windows\System32\ipconfig.exe";

        private static AppAccessValidator MakeValidator(
            List<WhiteListEntry>? whiteList = null,
            List<BlackListEntry>? blackList = null)
        {
            var config = new AppAccessConfig
            {
                WhiteList = whiteList ?? new(),
                BlackList = blackList ?? new()
            };
            return new AppAccessValidator(config);
        }

        // ═══════════════════════════════════════════════════════════════
        //  1. Empty config → allow everything
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void EmptyConfig_AllowsAnyApp()
        {
            var v = MakeValidator();
            var (allowed, _, _) = v.Validate(NotepadPath, null);
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void EmptyConfig_AllowsAnyArgs()
        {
            var v = MakeValidator();
            var (allowed, _, _) = v.Validate(IpconfigPath, "/all /flushdns");
            Assert.That(allowed, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════
        //  2. WhiteList with entries → only those apps allowed
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void WhiteList_AllowsListedApp()
        {
            var v = MakeValidator(whiteList: new()
            {
                new WhiteListEntry { Path = NotepadPath }
            });

            var (allowed, _, _) = v.Validate(NotepadPath, null);
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void WhiteList_BlocksUnlistedApp()
        {
            var v = MakeValidator(whiteList: new()
            {
                new WhiteListEntry { Path = NotepadPath }
            });

            var (allowed, _, reason) = v.Validate(CmdPath, null);
            Assert.That(allowed, Is.False);
            Assert.That(reason, Does.Contain("not in the whitelist"));
        }

        [Test]
        public void WhiteList_AllowedArgs_PermitsValidArg()
        {
            var v = MakeValidator(whiteList: new()
            {
                new WhiteListEntry { Path = IpconfigPath, AllowedArgs = new() { "/all", "/flushdns" } }
            });

            var (allowed, _, _) = v.Validate(IpconfigPath, "/all");
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void WhiteList_AllowedArgs_BlocksInvalidArg()
        {
            var v = MakeValidator(whiteList: new()
            {
                new WhiteListEntry { Path = IpconfigPath, AllowedArgs = new() { "/all" } }
            });

            var (allowed, _, reason) = v.Validate(IpconfigPath, "/release");
            Assert.That(allowed, Is.False);
            Assert.That(reason, Does.Contain("not in the allowed arguments"));
        }

        [Test]
        public void WhiteList_AllowedArgs_CaseInsensitive()
        {
            var v = MakeValidator(whiteList: new()
            {
                new WhiteListEntry { Path = IpconfigPath, AllowedArgs = new() { "/ALL" } }
            });

            var (allowed, _, _) = v.Validate(IpconfigPath, "/all");
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void WhiteList_EmptyAllowedArgs_PermitsAnyArg()
        {
            var v = MakeValidator(whiteList: new()
            {
                new WhiteListEntry { Path = IpconfigPath, AllowedArgs = new() }
            });

            var (allowed, _, _) = v.Validate(IpconfigPath, "/whatever /anything");
            Assert.That(allowed, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════
        //  3. BlackList (no whitelist) → block listed apps
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void BlackList_BlocksListedApp()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = CmdPath }
            });

            var (allowed, _, reason) = v.Validate(CmdPath, null);
            Assert.That(allowed, Is.False);
            Assert.That(reason, Does.Contain("blacklisted"));
        }

        [Test]
        public void BlackList_AllowsUnlistedApp()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = CmdPath }
            });

            var (allowed, _, _) = v.Validate(NotepadPath, null);
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void BlackList_RestrictedArgs_BlocksSpecificArg()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = IpconfigPath, RestrictedArgs = new() { "/flushdns" } }
            });

            var (allowed, _, reason) = v.Validate(IpconfigPath, "/flushdns");
            Assert.That(allowed, Is.False);
            Assert.That(reason.ToLower(), Does.Contain("restricted"));
        }

        [Test]
        public void BlackList_RestrictedArgs_AllowsOtherArgs()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = IpconfigPath, RestrictedArgs = new() { "/flushdns" } }
            });

            var (allowed, _, _) = v.Validate(IpconfigPath, "/all");
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void BlackList_RestrictedArgs_CaseInsensitive()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = IpconfigPath, RestrictedArgs = new() { "/FLUSHDNS" } }
            });

            var (allowed, _, _) = v.Validate(IpconfigPath, "/flushdns");
            Assert.That(allowed, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════
        //  4. Global restricted args (empty-path blacklist entry)
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void GlobalRestrictedArgs_NoWhiteList_BlocksArgForAnyApp()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = "", RestrictedArgs = new() { "/format" } }
            });

            var (allowed, _, reason) = v.Validate(NotepadPath, "/format");
            Assert.That(allowed, Is.False);
            Assert.That(reason, Does.Contain("globally restricted"));
        }

        [Test]
        public void GlobalRestrictedArgs_NoWhiteList_AllowsNonRestrictedArg()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = "", RestrictedArgs = new() { "/format" } }
            });

            var (allowed, _, _) = v.Validate(NotepadPath, "/safe");
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void GlobalRestrictedArgs_WithWhiteList_BlocksArgForWhiteListedApp()
        {
            // Scenario #3: WhiteList has app, BlackList has empty-path with restrictedArgs
            var v = MakeValidator(
                whiteList: new()
                {
                    new WhiteListEntry { Path = IpconfigPath }
                },
                blackList: new()
                {
                    new BlackListEntry { Path = "", RestrictedArgs = new() { "/flushdns" } }
                });

            var (allowed, _, reason) = v.Validate(IpconfigPath, "/flushdns");
            Assert.That(allowed, Is.False);
            Assert.That(reason, Does.Contain("globally restricted"));
        }

        [Test]
        public void GlobalRestrictedArgs_WithWhiteList_AllowsNonRestrictedArg()
        {
            var v = MakeValidator(
                whiteList: new()
                {
                    new WhiteListEntry { Path = IpconfigPath }
                },
                blackList: new()
                {
                    new BlackListEntry { Path = "", RestrictedArgs = new() { "/flushdns" } }
                });

            var (allowed, _, _) = v.Validate(IpconfigPath, "/all");
            Assert.That(allowed, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════
        //  5. Path traversal blocking
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void BlocksRelativePathTraversal_Backslash()
        {
            var v = MakeValidator();
            var (allowed, _, reason) = v.Validate(@"..\..\evil.exe", null);
            Assert.That(allowed, Is.False);
            Assert.That(reason.ToLower(), Does.Contain("traversal"));
        }

        [Test]
        public void BlocksRelativePathTraversal_ForwardSlash()
        {
            var v = MakeValidator();
            var (allowed, _, reason) = v.Validate("../../evil.exe", null);
            Assert.That(allowed, Is.False);
            Assert.That(reason.ToLower(), Does.Contain("traversal"));
        }

        // ═══════════════════════════════════════════════════════════════
        //  6. Empty / null appName
        // ═══════════════════════════════════════════════════════════════

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void EmptyAppName_IsBlocked(string? appName)
        {
            var v = MakeValidator();
            var (allowed, _, reason) = v.Validate(appName!, null);
            Assert.That(allowed, Is.False);
            Assert.That(reason.ToLower(), Does.Contain("empty"));
        }

        // ═══════════════════════════════════════════════════════════════
        //  7. Path resolution by short name (via where.exe)
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void ResolvesShortName_ViaPath()
        {
            // Short-name resolution goes through where.exe, which searches the *host process* PATH.
            // Resolving a system binary like "notepad" makes the test depend on the launching shell's
            // PATH ordering — e.g. a Git-Bash PATH resolves "notepad" to C:\Program Files\Git\usr\bin\notepad,
            // and Windows itself ships notepad.exe in both System32 and C:\Windows. To be deterministic
            // and host-independent, drop a uniquely-named dummy exe into a temp dir, put that dir first on
            // PATH, and assert the short name resolves to it. This exercises the real where.exe resolution
            // + whitelist matching without depending on any ambient system binary.
            var tempDir = Path.Combine(Path.GetTempPath(), "uiauto_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var exeName = "uiautotestapp_" + Guid.NewGuid().ToString("N")[..8];
            var exePath = Path.Combine(tempDir, exeName + ".exe");
            File.WriteAllBytes(exePath, new byte[] { 0x4D, 0x5A }); // "MZ" stub; where.exe only checks existence

            var originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            try
            {
                Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + originalPath);

                var v = MakeValidator(whiteList: new()
                {
                    new WhiteListEntry { Path = exePath }
                });

                var (allowed, resolvedPath, reason) = v.Validate(exeName, null);
                Assert.That(allowed, Is.True, $"resolved='{resolvedPath}' reason='{reason}'");
                Assert.That(resolvedPath, Is.EqualTo(exePath).IgnoreCase);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
            }
        }

        [Test]
        public void ResolvesShortName_IpconfigWithArgs()
        {
            var v = MakeValidator(whiteList: new()
            {
                new WhiteListEntry { Path = IpconfigPath, AllowedArgs = new() { "/all" } }
            });

            var (allowed, _, _) = v.Validate("ipconfig", "/all");
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void ResolvesShortName_IpconfigBlockedByBlacklist()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = IpconfigPath, RestrictedArgs = new() { "/flushdns" } }
            });

            var (allowed, _, _) = v.Validate("ipconfig", "/flushdns");
            Assert.That(allowed, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════
        //  8. Path comparison is case-insensitive
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void PathComparison_CaseInsensitive()
        {
            var v = MakeValidator(whiteList: new()
            {
                new WhiteListEntry { Path = @"C:\WINDOWS\SYSTEM32\NOTEPAD.EXE" }
            });

            var (allowed, _, _) = v.Validate(NotepadPath, null);
            Assert.That(allowed, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════
        //  9. Static utility tests
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void ContainsPathTraversal_DetectsDoubleDots()
        {
            Assert.That(AppAccessValidator.ContainsPathTraversal(@"..\..\evil.exe"), Is.True);
            Assert.That(AppAccessValidator.ContainsPathTraversal("../../evil.exe"), Is.True);
            Assert.That(AppAccessValidator.ContainsPathTraversal(@"C:\temp\..\evil.exe"), Is.True);
        }

        [Test]
        public void ContainsPathTraversal_AllowsNormalPaths()
        {
            Assert.That(AppAccessValidator.ContainsPathTraversal(@"C:\Windows\notepad.exe"), Is.False);
            Assert.That(AppAccessValidator.ContainsPathTraversal("notepad.exe"), Is.False);
            Assert.That(AppAccessValidator.ContainsPathTraversal("notepad"), Is.False);
        }

        [TestCase("/all /flushdns", new[] { "/all", "/flushdns" })]
        [TestCase("/all", new[] { "/all" })]
        [TestCase("", new string[0])]
        [TestCase(null, new string[0])]
        [TestCase("  /all   /flushdns  ", new[] { "/all", "/flushdns" })]
        public void ParseArguments_SplitsCorrectly(string? input, string[] expected)
        {
            var result = AppAccessValidator.ParseArguments(input);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ParseArguments_HandlesQuotedStrings()
        {
            var result = AppAccessValidator.ParseArguments("/c \"echo hello\" /all");
            Assert.That(result, Is.EqualTo(new[] { "/c", "echo hello", "/all" }));
        }

        // ═══════════════════════════════════════════════════════════════
        //  10. Combination scenarios (decision logic table)
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void Scenario1_EmptyWhiteList_EmptyBlackList_AllowAll()
        {
            var v = MakeValidator();
            Assert.That(v.Validate(NotepadPath, "/any").Allowed, Is.True);
            Assert.That(v.Validate(CmdPath, "/c dir").Allowed, Is.True);
            Assert.That(v.Validate(IpconfigPath, "/all").Allowed, Is.True);
        }

        [Test]
        public void Scenario2_EmptyWhiteList_BlackListHasEntries_AllowExceptBlackListed()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = CmdPath }
            });

            Assert.That(v.Validate(NotepadPath, null).Allowed, Is.True);
            Assert.That(v.Validate(IpconfigPath, "/all").Allowed, Is.True);
            Assert.That(v.Validate(CmdPath, null).Allowed, Is.False);
        }

        [Test]
        public void Scenario3_WhiteListHasApp_GlobalRestrictedArgs()
        {
            var v = MakeValidator(
                whiteList: new()
                {
                    new WhiteListEntry { Path = IpconfigPath }
                },
                blackList: new()
                {
                    new BlackListEntry { Path = "", RestrictedArgs = new() { "/flushdns" } }
                });

            // whitelisted app allowed
            Assert.That(v.Validate(IpconfigPath, "/all").Allowed, Is.True);
            // global arg restriction applies
            Assert.That(v.Validate(IpconfigPath, "/flushdns").Allowed, Is.False);
            // non-whitelisted app blocked
            Assert.That(v.Validate(NotepadPath, null).Allowed, Is.False);
        }

        [Test]
        public void Scenario4_EmptyWhiteList_GlobalRestrictedArgs()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = "", RestrictedArgs = new() { "/format", "--delete" } }
            });

            Assert.That(v.Validate(NotepadPath, null).Allowed, Is.True);
            Assert.That(v.Validate(IpconfigPath, "/all").Allowed, Is.True);
            Assert.That(v.Validate(NotepadPath, "/format").Allowed, Is.False);
            Assert.That(v.Validate(IpconfigPath, "--delete").Allowed, Is.False);
        }

        [Test]
        public void Scenario4_GlobalRestrictedArgs_CaseInsensitive()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = "", RestrictedArgs = new() { "/FORMAT" } }
            });

            Assert.That(v.Validate(NotepadPath, "/format").Allowed, Is.False);
            Assert.That(v.Validate(NotepadPath, "/Format").Allowed, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════
        //  11. Multiple args, partial block
        // ═══════════════════════════════════════════════════════════════

        [Test]
        public void MultipleArgs_OneRestricted_BlocksEntireRequest()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = IpconfigPath, RestrictedArgs = new() { "/flushdns" } }
            });

            // /all is fine, but /flushdns is restricted → blocked
            var (allowed, _, _) = v.Validate(IpconfigPath, "/all /flushdns");
            Assert.That(allowed, Is.False);
        }

        [Test]
        public void MultipleArgs_NoneRestricted_Allowed()
        {
            var v = MakeValidator(blackList: new()
            {
                new BlackListEntry { Path = IpconfigPath, RestrictedArgs = new() { "/flushdns" } }
            });

            var (allowed, _, _) = v.Validate(IpconfigPath, "/all /renew");
            Assert.That(allowed, Is.True);
        }
    }
}
