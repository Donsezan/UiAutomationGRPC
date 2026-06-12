using System.Diagnostics;
using System.Runtime.InteropServices;
using Trace = System.Diagnostics.Trace;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Resolves the PID that actually owns a launched application's window.
    ///
    /// Packaged (UWP/Store) apps launched via an alias (e.g. <c>calc</c>) start through a
    /// launcher/host process that exits almost immediately; <c>Process.Start</c> returns the
    /// launcher's PID, which is useless for PID-addressed RPCs once it exits. When the launched
    /// process dies within the grace window, this helper diffs the set of visible top-level
    /// windows taken before the launch against the current set and returns the PID of the
    /// newly-appeared window's owner. Classic Win32 apps never enter the resolution path.
    /// </summary>
    public static class UwpPidResolver
    {
        /// <summary>How long to wait for the launched process to either survive or exit.</summary>
        public const int LauncherGraceMs = 500;

        /// <summary>Total budget for finding the real window after the launcher exited.</summary>
        public const int ResolutionTimeoutMs = 3_000;

        /// <summary>Delay between window-set snapshots during resolution.</summary>
        public const int ResolutionPollMs = 150;

        /// <summary>
        /// Waits briefly on the launched process. If it survives the grace window it is a normal
        /// app and its own PID is returned. If it exits (UWP launcher behaviour), polls for a
        /// visible top-level window that was not present before the launch and returns its owner
        /// PID. Returns the original PID when nothing better could be resolved;
        /// <c>LauncherExited</c> tells the caller whether that original PID is already dead.
        /// </summary>
        public static (int Pid, bool Resolved, bool LauncherExited) ResolveLaunchedPid(
            Process? launched, HashSet<int> windowPidsBeforeLaunch)
        {
            int originalPid = launched?.Id ?? 0;
            if (launched == null) return (originalPid, false, false);

            try
            {
                if (!launched.WaitForExit(LauncherGraceMs))
                    return (originalPid, false, false); // still running — classic app, PID is good
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[UwpPidResolver] WaitForExit probe failed (PID={originalPid}): {ex.Message}");
                return (originalPid, false, false);
            }

            // Launcher exited. Look for a window that appeared since the pre-launch snapshot.
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < ResolutionTimeoutMs)
            {
                var current = VisibleTopLevelWindowPids();
                int candidate = PickNewWindowPid(windowPidsBeforeLaunch, current, GetStartTimeOrNull);
                if (candidate > 0)
                    return (candidate, true, true);

                Thread.Sleep(ResolutionPollMs);
            }

            return (originalPid, false, true);
        }

        /// <summary>
        /// Pure selection logic: among PIDs present now but not before the launch, pick the most
        /// recently started living process. Returns 0 when there is no candidate.
        /// </summary>
        public static int PickNewWindowPid(
            ISet<int> before,
            IEnumerable<int> after,
            Func<int, DateTime?> startTimeLookup)
        {
            var candidates = after.Where(pid => pid > 0 && !before.Contains(pid)).Distinct().ToList();
            if (candidates.Count == 0) return 0;
            if (candidates.Count == 1) return candidates[0];

            // Several new windows (background noise possible) — the most recently started
            // process is the best guess for "the app we just launched".
            return candidates
                .Select(pid => (Pid: pid, Start: startTimeLookup(pid)))
                .Where(c => c.Start.HasValue)
                .OrderByDescending(c => c.Start!.Value)
                .Select(c => c.Pid)
                .DefaultIfEmpty(candidates[0])
                .First();
        }

        private static DateTime? GetStartTimeOrNull(int pid)
        {
            try { return Process.GetProcessById(pid).StartTime; }
            catch { return null; } // exited, access denied, or not found
        }

        // ---------------------------------------------------------------- window snapshot

        /// <summary>PIDs owning currently visible top-level windows (cheap, no UIA involved).</summary>
        public static HashSet<int> VisibleTopLevelWindowPids()
        {
            var pids = new HashSet<int>();
            EnumWindows((hwnd, _) =>
            {
                if (IsWindowVisible(hwnd))
                {
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid > 0) pids.Add((int)pid);
                }
                return true;
            }, IntPtr.Zero);
            return pids;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
