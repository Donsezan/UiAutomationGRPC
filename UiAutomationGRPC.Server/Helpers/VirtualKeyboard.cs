using System.Runtime.InteropServices;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Keyboard input synthesis on raw <c>SendInput</c> (replacing the WinForms
    /// <c>System.Windows.Forms.SendKeys</c> wrapper). Same expression syntax — parsing is done by
    /// <see cref="SendKeysParser"/> — but with deterministic delivery:
    /// <list type="bullet">
    /// <item>Named keys and modifier chords go out as virtual-key + scan-code events
    /// (extended-key flag where required), which WinForms, UWP/XAML and Chromium-based apps
    /// all accept.</item>
    /// <item>Plain text goes out as <c>KEYEVENTF_UNICODE</c> events — keyboard-layout
    /// independent, so "Straße" or "Привет" types correctly on any layout.</item>
    /// <item>Characters under Ctrl/Alt are resolved through <c>VkKeyScan</c> so shortcuts like
    /// <c>^a</c> hit the real A key the way an accelerator table expects.</item>
    /// </list>
    /// </summary>
    public static class VirtualKeyboard
    {
        #region P/Invoke

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private const uint MAPVK_VK_TO_VSC = 0;

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_MENU = 0x12; // Alt

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type; // 1 = INPUT_KEYBOARD
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi; // sizes the union like the native one
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion

        /// <summary>VKs that need the extended-key flag (navigation cluster, arrows, etc.).</summary>
        private static readonly HashSet<ushort> ExtendedKeys = new()
        {
            0x21, 0x22, 0x23, 0x24,       // PGUP PGDN END HOME
            0x25, 0x26, 0x27, 0x28,       // LEFT UP RIGHT DOWN
            0x2C, 0x2D, 0x2E,             // PRTSC INS DEL
            0x90,                          // NUMLOCK
            0x5B, 0x5C, 0x5D,             // LWIN RWIN APPS
        };

        /// <summary>Pause between logical keystrokes; gives slow message pumps time to drain.</summary>
        private const int InterKeyDelayMs = 5;

        /// <summary>
        /// Sends a SendKeys-syntax expression. <paramref name="wait"/> adds a short trailing
        /// delay so the target's message queue can drain before the caller proceeds (the
        /// SendInput equivalent of the old <c>SendKeys.SendWait</c> behaviour).
        /// </summary>
        public static void Send(string keys, bool wait = true)
        {
            var tokens = SendKeysParser.Parse(keys);
            foreach (var token in tokens)
            {
                SendToken(token);
                Thread.Sleep(InterKeyDelayMs);
            }
            if (wait)
                Thread.Sleep(30);
        }

        /// <summary>Sends keys and waits for them to be processed (legacy API shape).</summary>
        public static void SendWait(string keys) => Send(keys, wait: true);

        /// <summary>Sends a key with a delay for keyboard readiness (legacy method).</summary>
        public static void SendKey(string buttonKey)
        {
            Thread.Sleep(UsabilityTimeLimits.KeyboardReadiness);
            SendWait(buttonKey);
        }

        /// <summary>Sends a key multiple times with delays between each press.</summary>
        public static void SendKey(string buttonKey, int count)
        {
            for (var i = 0; i < count; i++)
                SendKey(buttonKey);
        }

        private static void SendToken(KeyToken token)
        {
            var inputs = new List<INPUT>();

            var mods = token.Modifiers;
            char ch = token.Character;
            ushort vk = token.VirtualKey;

            // A literal character sent while a chord is held must press the physical key the
            // shortcut maps to, not inject a unicode char (apps resolve accelerators by VK).
            bool resolveCharToVk = !token.IsNamed && mods != KeyModifiers.None;
            if (resolveCharToVk)
            {
                short scan = VkKeyScan(ch);
                if (scan != -1)
                {
                    vk = (byte)(scan & 0xFF);
                    var shiftState = (scan >> 8) & 0x07;
                    if ((shiftState & 1) != 0) mods |= KeyModifiers.Shift; // e.g. ^A == Ctrl+Shift+a
                    if ((shiftState & 2) != 0) mods |= KeyModifiers.Ctrl;
                    if ((shiftState & 4) != 0) mods |= KeyModifiers.Alt;
                }
                // Unmappable char on this layout — fall back to unicode under the held chord.
            }

            AddModifiers(inputs, mods, down: true);

            for (int r = 0; r < token.Repeat; r++)
            {
                if (vk != 0)
                {
                    AddVk(inputs, vk, down: true);
                    AddVk(inputs, vk, down: false);
                }
                else
                {
                    AddUnicode(inputs, ch, down: true);
                    AddUnicode(inputs, ch, down: false);
                }
            }

            AddModifiers(inputs, mods, down: false);

            var array = inputs.ToArray();
            uint sent = SendInput((uint)array.Length, array, Marshal.SizeOf<INPUT>());
            if (sent != array.Length)
                throw new InvalidOperationException(
                    $"SendInput injected {sent}/{array.Length} events (error {Marshal.GetLastWin32Error()}). " +
                    "Input may be blocked by an elevated foreground window (run the server as Administrator).");
        }

        private static void AddModifiers(List<INPUT> inputs, KeyModifiers mods, bool down)
        {
            // Press in canonical order, release in reverse.
            var ordered = new List<ushort>(3);
            if (mods.HasFlag(KeyModifiers.Shift)) ordered.Add(VK_SHIFT);
            if (mods.HasFlag(KeyModifiers.Ctrl)) ordered.Add(VK_CONTROL);
            if (mods.HasFlag(KeyModifiers.Alt)) ordered.Add(VK_MENU);
            if (!down) ordered.Reverse();

            foreach (var vk in ordered)
                AddVk(inputs, vk, down);
        }

        private static void AddVk(List<INPUT> inputs, ushort vk, bool down)
        {
            uint flags = down ? 0u : KEYEVENTF_KEYUP;
            if (ExtendedKeys.Contains(vk))
                flags |= KEYEVENTF_EXTENDEDKEY;

            inputs.Add(new INPUT
            {
                type = 1,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC),
                        dwFlags = flags
                    }
                }
            });
        }

        private static void AddUnicode(List<INPUT> inputs, char ch, bool down)
        {
            inputs.Add(new INPUT
            {
                type = 1,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = ch,
                        dwFlags = KEYEVENTF_UNICODE | (down ? 0u : KEYEVENTF_KEYUP)
                    }
                }
            });
        }
    }
}
