using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using UiAutomationGRPC.Server.Handlers;
using Trace = System.Diagnostics.Trace;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Best-effort foreground activation for the window that owns a target element.
    ///
    /// <c>AutomationElement.Focus()</c> alone cannot bring a background window forward —
    /// Windows' foreground-lock rules silently ignore it, so synthesized keys/clicks land in the
    /// wrong app (the classic "send_keys did nothing" failure). Restore the window if minimized,
    /// try <c>SetForegroundWindow</c>, and when the lock denies it, attach to the current
    /// foreground thread's input queue and retry — the documented escape hatch.
    /// </summary>
    public static class WindowFocus
    {
        /// <summary>
        /// True when the element's top-level window currently holds the foreground (or when it has
        /// no resolvable HWND, in which case foreground state cannot be checked and input is
        /// allowed through). Used to refuse keyboard injection that would land in another app.
        /// </summary>
        public static bool IsForeground(AutomationElement element)
        {
            try
            {
                var window = ScreenshotHandler.GetTopLevelWindow(element) ?? element;
                IntPtr handle = window.Properties.NativeWindowHandle.ValueOrDefault;
                if (handle == IntPtr.Zero) return true; // windowless host — cannot verify, do not block

                IntPtr fg = GetForegroundWindow();
                if (fg == handle) return true;

                // The foreground window may be a child/owned popup of the same process tree
                // (e.g. a dialog) — same app, still fine.
                GetWindowThreadProcessId(fg, out uint fgPid);
                int targetPid = window.Properties.ProcessId.ValueOrDefault;
                return fgPid == targetPid;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WindowFocus] IsForeground check failed (assuming ok): {ex.Message}");
                return true;
            }
        }

        public static void EnsureForeground(AutomationElement element)
        {
            try
            {
                var window = ScreenshotHandler.GetTopLevelWindow(element) ?? element;

                IntPtr hwnd = IntPtr.Zero;
                try { hwnd = window.Properties.NativeWindowHandle.ValueOrDefault; }
                catch (Exception ex) { Trace.WriteLine($"[WindowFocus] NativeWindowHandle read failed: {ex.Message}"); }
                if (hwnd == IntPtr.Zero) return; // windowless host (some XAML islands) — nothing to activate

                if (GetForegroundWindow() == hwnd) return;

                if (IsIconic(hwnd))
                    ShowWindow(hwnd, SW_RESTORE);

                // UIA2's managed SetFocus used the client-side Win32 proxy, which raised the
                // window to the foreground as a side effect — UIA3's native SetFocus does not,
                // so this activation must succeed on its own (the SendKeys foreground check
                // refuses to type otherwise).
                if (SetForegroundWindow(hwnd) && GetForegroundWindow() == hwnd) return;

                // Foreground lock denied the switch — attach to the input queues of BOTH the
                // current foreground owner and the target window, then retry. Attaching only
                // to the foreground thread is often not enough for BringWindowToTop to take.
                uint ourThread = GetCurrentThreadId();
                IntPtr currentFg = GetForegroundWindow();
                uint fgThread = currentFg != IntPtr.Zero ? GetWindowThreadProcessId(currentFg, out _) : 0;
                uint targetThread = GetWindowThreadProcessId(hwnd, out _);

                bool fgAttached = fgThread != 0 && fgThread != ourThread
                                  && AttachThreadInput(ourThread, fgThread, true);
                bool targetAttached = targetThread != 0 && targetThread != ourThread && targetThread != fgThread
                                      && AttachThreadInput(ourThread, targetThread, true);
                try
                {
                    BringWindowToTop(hwnd);
                    SetForegroundWindow(hwnd);
                }
                finally
                {
                    if (targetAttached) AttachThreadInput(ourThread, targetThread, false);
                    if (fgAttached) AttachThreadInput(ourThread, fgThread, false);
                }

                if (GetForegroundWindow() == hwnd) return;

                // Last resort: the ALT-key trick — an in-flight ALT keypress releases the
                // foreground lock for the caller (the classic automation-framework workaround).
                // ALT is released immediately, so no modifier state leaks into later input.
                keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                try
                {
                    SetForegroundWindow(hwnd);
                }
                finally
                {
                    keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                // Never fail the action because activation didn't work — Focus may still suffice.
                Trace.WriteLine($"[WindowFocus] EnsureForeground failed (non-fatal): {ex.Message}");
            }
        }

        private const int SW_RESTORE = 9;
        private const byte VK_MENU = 0x12;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    }
}
