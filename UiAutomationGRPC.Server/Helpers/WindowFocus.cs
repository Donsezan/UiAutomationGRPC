using System.Runtime.InteropServices;
using System.Windows.Automation;
using UiAutomationGRPC.Server.Handlers;
using Trace = System.Diagnostics.Trace;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Best-effort foreground activation for the window that owns a target element.
    ///
    /// <c>AutomationElement.SetFocus()</c> alone cannot bring a background window forward —
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
                int handle = (int)window.GetCurrentPropertyValue(AutomationElement.NativeWindowHandleProperty);
                if (handle == 0) return true; // windowless host — cannot verify, do not block

                IntPtr fg = GetForegroundWindow();
                if (fg == new IntPtr(handle)) return true;

                // The foreground window may be a child/owned popup of the same process tree
                // (e.g. a dialog) — same app, still fine.
                GetWindowThreadProcessId(fg, out uint fgPid);
                int targetPid = (int)window.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty);
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

                int handle = 0;
                try { handle = (int)window.GetCurrentPropertyValue(AutomationElement.NativeWindowHandleProperty); }
                catch (Exception ex) { Trace.WriteLine($"[WindowFocus] NativeWindowHandle read failed: {ex.Message}"); }
                if (handle == 0) return; // windowless host (some XAML islands) — nothing to activate

                var hwnd = new IntPtr(handle);
                if (GetForegroundWindow() == hwnd) return;

                if (IsIconic(hwnd))
                    ShowWindow(hwnd, SW_RESTORE);

                if (SetForegroundWindow(hwnd)) return;

                // Foreground lock denied the switch — attach to the owner of the current
                // foreground window's input queue and retry.
                IntPtr currentFg = GetForegroundWindow();
                if (currentFg == IntPtr.Zero) return;

                uint fgThread = GetWindowThreadProcessId(currentFg, out _);
                uint ourThread = GetCurrentThreadId();
                if (fgThread != ourThread && AttachThreadInput(ourThread, fgThread, true))
                {
                    try { SetForegroundWindow(hwnd); }
                    finally { AttachThreadInput(ourThread, fgThread, false); }
                }
            }
            catch (Exception ex)
            {
                // Never fail the action because activation didn't work — SetFocus may still suffice.
                Trace.WriteLine($"[WindowFocus] EnsureForeground failed (non-fatal): {ex.Message}");
            }
        }

        private const int SW_RESTORE = 9;

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
