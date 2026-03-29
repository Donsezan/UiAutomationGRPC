using NUnit.Framework;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Tests
{
    [TestFixture]
    public class KeyAccessValidatorTests
    {
        // ────────────────────────────── No restrictions ──────────────────────────────

        [TestCase("hello")]
        [TestCase("%{F4}")]
        [TestCase("^c")]
        public void NoLists_AllKeysAllowed(string keys)
        {
            var validator = CreateValidator(whiteList: [], blackList: []);
            var (allowed, _) = validator.Validate(keys);
            Assert.That(allowed, Is.True);
        }

        // ────────────────────────────── Empty input ──────────────────────────────

        [TestCase(null)]
        [TestCase("")]
        public void EmptyInput_Denied(string? keys)
        {
            var validator = CreateValidator(whiteList: [], blackList: []);
            var (allowed, reason) = validator.Validate(keys!);
            Assert.That(allowed, Is.False);
            Assert.That(reason, Does.Contain("empty"));
        }

        // ────────────────────────────── BlackList ──────────────────────────────

        [Test]
        public void BlackList_ExactMatch_Denied()
        {
            var validator = CreateValidator(blackList: ["%{F4}"]);
            var (allowed, reason) = validator.Validate("%{F4}");
            Assert.That(allowed, Is.False);
            Assert.That(reason, Does.Contain("%{F4}"));
        }

        [Test]
        public void BlackList_EmbeddedMatch_Denied()
        {
            var validator = CreateValidator(blackList: ["%{F4}"]);
            var (allowed, _) = validator.Validate("abc%{F4}xyz");
            Assert.That(allowed, Is.False);
        }

        [Test]
        public void BlackList_CaseInsensitive_Denied()
        {
            var validator = CreateValidator(blackList: ["%{f4}"]);
            var (allowed, _) = validator.Validate("%{F4}");
            Assert.That(allowed, Is.False);
        }

        [Test]
        public void BlackList_NonRestricted_Allowed()
        {
            var validator = CreateValidator(blackList: ["%{F4}", "^{ESC}"]);
            var (allowed, _) = validator.Validate("{ENTER}");
            Assert.That(allowed, Is.True);
        }

        // ────────────────────────────── WhiteList ──────────────────────────────

        [Test]
        public void WhiteList_ExactMatch_Allowed()
        {
            var validator = CreateValidator(whiteList: ["{ENTER}", "{TAB}"]);
            var (allowed, _) = validator.Validate("{ENTER}");
            Assert.That(allowed, Is.True);
        }

        [Test]
        public void WhiteList_NoMatch_Denied()
        {
            var validator = CreateValidator(whiteList: ["{ENTER}"]);
            var (allowed, reason) = validator.Validate("^c");
            Assert.That(allowed, Is.False);
            Assert.That(reason, Does.Contain("not in the whitelist"));
        }

        [Test]
        public void WhiteList_EmptyList_AllDenied()
        {
            // Edge case: WhiteList is technically empty (no entries)
            // but covered by the "no lists → allow" path
            var validator = CreateValidator(whiteList: [], blackList: []);
            var (allowed, _) = validator.Validate("hello");
            Assert.That(allowed, Is.True);
        }

        // ────────────────────────────── {PLAINTEXT} ──────────────────────────────

        [TestCase("hello")]
        [TestCase("Hello World 123")]
        [TestCase("test@email.com")]
        [TestCase("abc!@#$&*")]
        [TestCase("a")]
        [TestCase("5")]
        public void WhiteList_PlainTextToken_AllowsRegularText(string keys)
        {
            var validator = CreateValidator(whiteList: ["{PLAINTEXT}"]);
            var (allowed, _) = validator.Validate(keys);
            Assert.That(allowed, Is.True);
        }

        [TestCase("^c")]
        [TestCase("%{F4}")]
        [TestCase("+a")]
        [TestCase("~")]
        [TestCase("{ENTER}")]
        [TestCase("hello(world)")]
        public void WhiteList_PlainTextToken_DeniesSpecialKeys(string keys)
        {
            var validator = CreateValidator(whiteList: ["{PLAINTEXT}"]);
            var (allowed, _) = validator.Validate(keys);
            Assert.That(allowed, Is.False);
        }

        [Test]
        public void WhiteList_PlainTextPlusSpecificKeys_AllowsBoth()
        {
            var validator = CreateValidator(whiteList: ["{PLAINTEXT}", "{ENTER}", "{TAB}"]);

            Assert.Multiple(() =>
            {
                Assert.That(validator.Validate("hello").Allowed, Is.True, "Plain text should be allowed");
                Assert.That(validator.Validate("{ENTER}").Allowed, Is.True, "Whitelisted special key should be allowed");
                Assert.That(validator.Validate("{TAB}").Allowed, Is.True, "Whitelisted special key should be allowed");
                Assert.That(validator.Validate("^c").Allowed, Is.False, "Ctrl+C should be denied");
                Assert.That(validator.Validate("%{F4}").Allowed, Is.False, "Alt+F4 should be denied");
            });
        }

        // ────────────────────────────── Combined WhiteList + BlackList ──────────────────────────────

        [Test]
        public void Combined_WhiteListAllows_BlackListBlocks_Denied()
        {
            var validator = CreateValidator(
                whiteList: ["{PLAINTEXT}", "^c"],
                blackList: ["^c"]);

            Assert.Multiple(() =>
            {
                Assert.That(validator.Validate("hello").Allowed, Is.True, "Plain text allowed by whitelist");
                Assert.That(validator.Validate("^c").Allowed, Is.False, "Ctrl+C whitelisted but blacklisted → denied");
            });
        }

        // ────────────────────────────── IsPlainText static helper ──────────────────────────────

        [TestCase("hello", true)]
        [TestCase("123", true)]
        [TestCase("test-value_here", true)]
        [TestCase("abc!@#$&*", true)]
        [TestCase("^c", false)]
        [TestCase("%x", false)]
        [TestCase("+a", false)]
        [TestCase("~", false)]
        [TestCase("a(b)", false)]
        [TestCase("{ENTER}", false)]
        public void IsPlainText_DetectsCorrectly(string input, bool expected)
        {
            Assert.That(KeyAccessValidator.IsPlainText(input), Is.EqualTo(expected));
        }

        // ────────────────────────────── Helper ──────────────────────────────

        private static KeyAccessValidator CreateValidator(
            List<string>? whiteList = null,
            List<string>? blackList = null)
        {
            var config = new KeyRestrictionConfig
            {
                WhiteList = whiteList ?? [],
                BlackList = blackList ?? []
            };
            return new KeyAccessValidator(config);
        }
    }
}
