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
    ///
    /// The diff is by window HANDLE, not by owner PID: UWP windows are hosted by the long-lived
    /// ApplicationFrameHost, whose PID is almost always present before the launch too — a
    /// PID-level diff would never see the new window (real failure observed with Calculator).
    /// </summary>
    public static class UwpPidResolver
    {
        /// <summary>
        /// How long a still-running but window-less launched process is watched before it is
        /// accepted as a background/CLI app and its own PID returned. A fixed "survived the
        /// grace" check is not enough: the calc.exe alias stub stays alive ~1 s before handing
        /// off to the Store app and exiting — longer than any reasonable fixed grace.
        /// </summary>
        public const int AliveWindowlessGraceMs = 1_500;

        /// <summary>
        /// Total budget for finding the real window after the launcher exited. Generous because a
        /// cold UWP start can take several seconds (Calculator: 5-6 s observed); resolution
        /// returns as soon as the window appears, so fast apps never pay the full budget.
        /// </summary>
        public const int ResolutionTimeoutMs = 10_000;

        /// <summary>Delay between window-set snapshots during resolution.</summary>
        public const int ResolutionPollMs = 150;

        /// <summary>
        /// Debounce before trusting a newly-appeared window. UWP startup briefly shows the app's
        /// CoreWindow as a visible top-level window before reparenting it into the
        /// ApplicationFrameHost frame — sampling once can return a PID whose window (and
        /// sometimes process) is gone moments later. After the settle delay the frame window is
        /// what remains.
        /// </summary>
        public const int WindowSettleMs = 300;

        /// <summary>
        /// Decides on evidence rather than a fixed grace period:
        /// <list type="bullet">
        /// <item>launched process is alive and owns a newly-visible window → classic app, its own
        /// PID is returned (as soon as the window shows, usually well under a second);</item>
        /// <item>alive but window-less past <see cref="AliveWindowlessGraceMs"/> → background/CLI
        /// app, its own PID is returned;</item>
        /// <item>exited (UWP launcher behaviour) → polls for a window HANDLE that was not in the
        /// pre-launch snapshot and returns its owner PID (debounced, because UWP startup briefly
        /// shows the app's CoreWindow top-level before reparenting it into the frame).</item>
        /// </list>
        /// Returns the original PID when nothing better could be resolved;
        /// <c>LauncherExited</c> tells the caller whether that original PID is already dead.
        /// </summary>
        public static (int Pid, bool Resolved, bool LauncherExited) ResolveLaunchedPid(
            Process? launched, HashSet<IntPtr> windowsBeforeLaunch)
        {
            int originalPid = launched?.Id ?? 0;
            if (launched == null) return (originalPid, false, false);

            var noPriorPids = new HashSet<int>();
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                bool exited;
                try
                {
                    exited = launched.HasExited;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[UwpPidResolver] HasExited probe failed (PID={originalPid}): {ex.Message}");
                    return (originalPid, false, false);
                }

                if (!exited)
                {
                    if (PidsOfNewVisibleWindows(windowsBeforeLaunch).Contains(originalPid))
                        return (originalPid, false, false); // showed its own window — classic app

                    if (stopwatch.ElapsedMilliseconds >= AliveWindowlessGraceMs)
                        return (originalPid, false, false); // alive, window-less — background/CLI app
                }
                else
                {
                    if (stopwatch.ElapsedMilliseconds >= ResolutionTimeoutMs)
                        return (originalPid, false, true);

                    var firstSample = PidsOfNewVisibleWindows(windowsBeforeLaunch);
                    if (firstSample.Count > 0)
                    {
                        // Something appeared — debounce, then prefer what is STILL there (the
                        // frame window), falling back to the first sample if it flickered away.
                        Thread.Sleep(WindowSettleMs);
                        var settled = PidsOfNewVisibleWindows(windowsBeforeLaunch);
                        var sample = settled.Count > 0 ? settled : firstSample;

                        int candidate = PickNewWindowPid(noPriorPids, sample, GetStartTimeOrNull);
                        if (candidate > 0 && IsAlive(candidate))
                            return (candidate, true, true);
                    }
                }

                Thread.Sleep(ResolutionPollMs);
            }
        }

        private static bool IsAlive(int pid)
        {
            try { return !Process.GetProcessById(pid).HasExited; }
            catch { return false; }
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

        /// <summary>Handles of currently visible top-level windows (cheap, no UIA involved).</summary>
        public static HashSet<IntPtr> VisibleTopLevelWindows()
        {
            var windows = new HashSet<IntPtr>();
            EnumWindows((hwnd, _) =>
            {
                if (IsWindowVisible(hwnd))
                    windows.Add(hwnd);
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        /// <summary>Owner PIDs of visible top-level windows that were not in the snapshot.</summary>
        public static List<int> PidsOfNewVisibleWindows(HashSet<IntPtr> before)
        {
            var pids = new List<int>();
            EnumWindows((hwnd, _) =>
            {
                if (IsWindowVisible(hwnd) && !before.Contains(hwnd))
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
