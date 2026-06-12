using NUnit.Framework;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Tests
{
    [TestFixture]
    public class SendKeysParserTests
    {
        private const ushort VK_RETURN = 0x0D;
        private const ushort VK_LEFT = 0x25;
        private const ushort VK_F5 = 0x74;
        private const ushort VK_DELETE = 0x2E;

        // ────────────────────────────── plain text ──────────────────────────────

        [Test]
        public void Parse_PlainText_OneTokenPerCharacter()
        {
            var tokens = SendKeysParser.Parse("ab1");

            Assert.Multiple(() =>
            {
                Assert.That(tokens, Has.Count.EqualTo(3));
                Assert.That(tokens.Select(t => t.Character), Is.EqualTo(new[] { 'a', 'b', '1' }));
                Assert.That(tokens.All(t => !t.IsNamed && t.Modifiers == KeyModifiers.None && t.Repeat == 1), Is.True);
            });
        }

        [Test]
        public void Parse_UnicodeText_IsPreserved()
        {
            var tokens = SendKeysParser.Parse("ß я");
            Assert.That(tokens.Select(t => t.Character), Is.EqualTo(new[] { 'ß', ' ', 'я' }));
        }

        [Test]
        public void Parse_EmptyString_NoTokens()
        {
            Assert.That(SendKeysParser.Parse(""), Is.Empty);
        }

        // ────────────────────────────── named keys ──────────────────────────────

        [TestCase("{ENTER}", VK_RETURN)]
        [TestCase("{enter}", VK_RETURN)]
        [TestCase("{F5}", VK_F5)]
        [TestCase("{DELETE}", VK_DELETE)]
        [TestCase("{DEL}", VK_DELETE)]
        public void Parse_NamedKey_MapsToVirtualKey(string expr, int expectedVk)
        {
            var tokens = SendKeysParser.Parse(expr);

            Assert.Multiple(() =>
            {
                Assert.That(tokens, Has.Count.EqualTo(1));
                Assert.That(tokens[0].IsNamed, Is.True);
                Assert.That(tokens[0].VirtualKey, Is.EqualTo((ushort)expectedVk));
            });
        }

        [Test]
        public void Parse_Tilde_IsEnter()
        {
            var tokens = SendKeysParser.Parse("~");
            Assert.That(tokens.Single().VirtualKey, Is.EqualTo(VK_RETURN));
        }

        [Test]
        public void Parse_RepeatCount_IsApplied()
        {
            var tokens = SendKeysParser.Parse("{LEFT 5}");

            Assert.Multiple(() =>
            {
                Assert.That(tokens.Single().VirtualKey, Is.EqualTo(VK_LEFT));
                Assert.That(tokens.Single().Repeat, Is.EqualTo(5));
            });
        }

        [Test]
        public void Parse_RepeatCount_OnLiteralCharacter()
        {
            var tokens = SendKeysParser.Parse("{x 3}");

            Assert.Multiple(() =>
            {
                Assert.That(tokens.Single().Character, Is.EqualTo('x'));
                Assert.That(tokens.Single().IsNamed, Is.False);
                Assert.That(tokens.Single().Repeat, Is.EqualTo(3));
            });
        }

        // ────────────────────────────── modifiers ──────────────────────────────

        [Test]
        public void Parse_CtrlA_ModifierAppliesToNextKeyOnly()
        {
            var tokens = SendKeysParser.Parse("^ab");

            Assert.Multiple(() =>
            {
                Assert.That(tokens, Has.Count.EqualTo(2));
                Assert.That(tokens[0].Modifiers, Is.EqualTo(KeyModifiers.Ctrl));
                Assert.That(tokens[1].Modifiers, Is.EqualTo(KeyModifiers.None));
            });
        }

        [Test]
        public void Parse_StackedModifiers_Combine()
        {
            var tokens = SendKeysParser.Parse("+^%x");
            Assert.That(tokens.Single().Modifiers,
                Is.EqualTo(KeyModifiers.Shift | KeyModifiers.Ctrl | KeyModifiers.Alt));
        }

        [Test]
        public void Parse_ModifierBeforeNamedKey()
        {
            var tokens = SendKeysParser.Parse("%{F4}");

            Assert.Multiple(() =>
            {
                Assert.That(tokens.Single().Modifiers, Is.EqualTo(KeyModifiers.Alt));
                Assert.That(tokens.Single().VirtualKey, Is.EqualTo((ushort)0x73));
            });
        }

        [Test]
        public void Parse_Group_ModifierAppliesToAllMembers()
        {
            var tokens = SendKeysParser.Parse("+(ab)c");

            Assert.Multiple(() =>
            {
                Assert.That(tokens, Has.Count.EqualTo(3));
                Assert.That(tokens[0].Modifiers, Is.EqualTo(KeyModifiers.Shift));
                Assert.That(tokens[1].Modifiers, Is.EqualTo(KeyModifiers.Shift));
                Assert.That(tokens[2].Modifiers, Is.EqualTo(KeyModifiers.None));
            });
        }

        // ────────────────────────────── escapes ──────────────────────────────

        [TestCase("{+}", '+')]
        [TestCase("{^}", '^')]
        [TestCase("{%}", '%')]
        [TestCase("{~}", '~')]
        [TestCase("{(}", '(')]
        [TestCase("{)}", ')')]
        [TestCase("{{}", '{')]
        [TestCase("{}}", '}')]
        public void Parse_EscapedSpecialCharacters_AreLiterals(string expr, char expected)
        {
            var tokens = SendKeysParser.Parse(expr);

            Assert.Multiple(() =>
            {
                Assert.That(tokens.Single().Character, Is.EqualTo(expected));
                Assert.That(tokens.Single().IsNamed, Is.False);
            });
        }

        [Test]
        public void Parse_MixedExpression_RealWorldSaveShortcutAndText()
        {
            // ^s then "hi" then ENTER — a typical agent sequence.
            var tokens = SendKeysParser.Parse("^shi{ENTER}");

            Assert.Multiple(() =>
            {
                Assert.That(tokens, Has.Count.EqualTo(4));
                Assert.That(tokens[0].Character, Is.EqualTo('s'));
                Assert.That(tokens[0].Modifiers, Is.EqualTo(KeyModifiers.Ctrl));
                Assert.That(tokens[1].Character, Is.EqualTo('h'));
                Assert.That(tokens[2].Character, Is.EqualTo('i'));
                Assert.That(tokens[3].VirtualKey, Is.EqualTo(VK_RETURN));
            });
        }

        // ────────────────────────────── errors ──────────────────────────────

        [TestCase("{NOSUCHKEY}")]
        [TestCase("{")]
        [TestCase("(ab")]
        [TestCase(")")]
        [TestCase("}")]
        [TestCase("{}")]
        [TestCase("^")]
        [TestCase("{LEFT zero}")]
        [TestCase("{LEFT 0}")]
        public void Parse_MalformedExpression_Throws(string expr)
        {
            Assert.Throws<ArgumentException>(() => SendKeysParser.Parse(expr));
        }

        [Test]
        public void Parse_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => SendKeysParser.Parse(null!));
        }
    }
}
