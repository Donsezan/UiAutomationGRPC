namespace UiAutomationGRPC.Server.Helpers
{
    [Flags]
    public enum KeyModifiers
    {
        None = 0,
        Shift = 1,
        Ctrl = 2,
        Alt = 4
    }

    /// <summary>
    /// One logical keystroke: either a literal character (VirtualKey == 0) or a named key
    /// (VirtualKey != 0), with the modifiers that must be held while it is pressed and a
    /// repeat count (from the "{LEFT 5}" form).
    /// </summary>
    public readonly struct KeyToken
    {
        public char Character { get; init; }
        public ushort VirtualKey { get; init; }
        public int Repeat { get; init; }
        public KeyModifiers Modifiers { get; init; }
        public bool IsNamed => VirtualKey != 0;
    }

    /// <summary>
    /// Parses the classic SendKeys syntax (<c>{ENTER}</c>, <c>^a</c>, <c>+%(xy)</c>, <c>{LEFT 5}</c>,
    /// escaped <c>{{}</c>/<c>{}}</c>/<c>{+}</c>…) into <see cref="KeyToken"/>s, independent of any
    /// input API. The parser is pure so the grammar is fully unit-testable; the SendInput plumbing
    /// lives in <see cref="VirtualKeyboard"/>.
    /// </summary>
    public static class SendKeysParser
    {
        private static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ENTER"] = 0x0D,
            ["TAB"] = 0x09,
            ["ESC"] = 0x1B,
            ["ESCAPE"] = 0x1B,
            ["BACKSPACE"] = 0x08,
            ["BS"] = 0x08,
            ["BKSP"] = 0x08,
            ["DEL"] = 0x2E,
            ["DELETE"] = 0x2E,
            ["INS"] = 0x2D,
            ["INSERT"] = 0x2D,
            ["HOME"] = 0x24,
            ["END"] = 0x23,
            ["PGUP"] = 0x21,
            ["PGDN"] = 0x22,
            ["UP"] = 0x26,
            ["DOWN"] = 0x28,
            ["LEFT"] = 0x25,
            ["RIGHT"] = 0x27,
            ["SPACE"] = 0x20,
            ["CAPSLOCK"] = 0x14,
            ["NUMLOCK"] = 0x90,
            ["SCROLLLOCK"] = 0x91,
            ["PRTSC"] = 0x2C,
            ["BREAK"] = 0x03,
            ["HELP"] = 0x2F,
            ["LWIN"] = 0x5B,
            ["RWIN"] = 0x5C,
            ["APPS"] = 0x5D,
        };

        private const ushort VkReturn = 0x0D;

        static SendKeysParser()
        {
            for (int f = 1; f <= 24; f++)
                NamedKeys[$"F{f}"] = (ushort)(0x70 + f - 1);
        }

        public static List<KeyToken> Parse(string keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));

            var tokens = new List<KeyToken>();
            var pending = KeyModifiers.None;
            int i = 0;

            while (i < keys.Length)
            {
                char c = keys[i];
                switch (c)
                {
                    case '+': pending |= KeyModifiers.Shift; i++; break;
                    case '^': pending |= KeyModifiers.Ctrl; i++; break;
                    case '%': pending |= KeyModifiers.Alt; i++; break;

                    case '~':
                        tokens.Add(new KeyToken { VirtualKey = VkReturn, Repeat = 1, Modifiers = pending });
                        pending = KeyModifiers.None;
                        i++;
                        break;

                    case '(':
                    {
                        int close = keys.IndexOf(')', i + 1);
                        if (close < 0) throw new ArgumentException("Unmatched '(' in SendKeys expression.");
                        // Modifiers apply to every character of the group: +%(xy) == Shift+Alt+x, Shift+Alt+y.
                        foreach (char gc in keys.AsSpan(i + 1, close - i - 1))
                            tokens.Add(CharToken(gc, pending));
                        pending = KeyModifiers.None;
                        i = close + 1;
                        break;
                    }

                    case ')':
                        throw new ArgumentException("Unmatched ')' in SendKeys expression.");

                    case '{':
                    {
                        int end = keys.IndexOf('}', i + 1);
                        // "{}}" — the body itself is '}', so the real terminator is one further.
                        if (end == i + 1 && i + 2 < keys.Length && keys[i + 2] == '}')
                            end = i + 2;
                        if (end < 0) throw new ArgumentException("Unmatched '{' in SendKeys expression.");

                        string body = keys.Substring(i + 1, end - i - 1);
                        tokens.Add(ParseBraced(body, pending));
                        pending = KeyModifiers.None;
                        i = end + 1;
                        break;
                    }

                    case '}':
                        throw new ArgumentException("Unmatched '}' in SendKeys expression.");

                    default:
                        tokens.Add(CharToken(c, pending));
                        pending = KeyModifiers.None;
                        i++;
                        break;
                }
            }

            if (pending != KeyModifiers.None)
                throw new ArgumentException("Dangling modifier at end of SendKeys expression.");

            return tokens;
        }

        private static KeyToken ParseBraced(string body, KeyModifiers mods)
        {
            if (body.Length == 0)
                throw new ArgumentException("Empty '{}' in SendKeys expression.");

            // Single character body: the escapes {{} {}} {+} {^} {%} {~} {(} {)} and any literal char.
            if (body.Length == 1)
                return CharToken(body[0], mods);

            string name = body;
            int repeat = 1;

            int space = body.IndexOf(' ');
            if (space > 0)
            {
                name = body[..space];
                string countPart = body[(space + 1)..].Trim();
                if (!int.TryParse(countPart, out repeat) || repeat < 1)
                    throw new ArgumentException($"Invalid repeat count '{countPart}' in '{{{body}}}'.");
            }

            if (name.Length == 1)
                return CharToken(name[0], mods) with { Repeat = repeat };

            if (!NamedKeys.TryGetValue(name, out ushort vk))
                throw new ArgumentException($"Unknown key name '{name}' in SendKeys expression.");

            return new KeyToken { VirtualKey = vk, Repeat = repeat, Modifiers = mods };
        }

        private static KeyToken CharToken(char c, KeyModifiers mods) =>
            new() { Character = c, Repeat = 1, Modifiers = mods };
    }
}
