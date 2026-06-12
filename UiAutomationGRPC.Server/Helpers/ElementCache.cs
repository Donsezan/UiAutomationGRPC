using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using Trace = System.Diagnostics.Trace;
using PropertyCondition = System.Windows.Automation.PropertyCondition;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Thread-safe element registry that maps a <c>RuntimeId</c> handle back to a LIVE
    /// <see cref="AutomationElement"/>. It stores locator metadata (PID + AutomationId/Name/ClassName)
    /// alongside each reference so an element that has gone stale — or that must be re-read fresh —
    /// can be re-located from the live UI tree.
    ///
    /// This is a <b>handle registry with re-find</b>, not a property-value cache: handlers always read
    /// property values live at call time. RuntimeId resolution therefore works in <i>both</i> modes:
    ///
    /// <list type="bullet">
    /// <item><b>Enabled</b> (default): persisted handles are trusted via a fast liveness probe and only
    /// re-located when they go stale. Fastest for repeated access to the same element.</item>
    /// <item><b>Disabled</b>: every access re-resolves the element from the current live tree (honouring
    /// the "parse the live UI tree per request" contract), falling back to the persisted handle only
    /// when a scoped re-find can't locate it. Slower, but always reflects the live tree.</item>
    /// </list>
    ///
    /// Disabling the cache no longer breaks cross-call addressing — it only changes the resolution
    /// strategy. For dynamic UIs the enabled mode is usually preferable because it already re-finds
    /// obsolete elements automatically.
    /// </summary>
    public static class ElementCache
    {
        /// <summary>
        /// A cached element with its locator info for re-finding.
        /// </summary>
        public class CachedElement
        {
            public AutomationElement Element { get; set; }
            public string AutomationId { get; set; }
            public string Name { get; set; }
            public string ClassName { get; set; }
            public string ControlTypeName { get; set; }
            public int ProcessId { get; set; }
        }

        private static readonly ConcurrentDictionary<string, CachedElement> _cache = new();

        /// <summary>
        /// Selects the RuntimeId resolution strategy (see the type summary).
        /// When <c>true</c> persisted handles are trusted and reused; when <c>false</c> every access
        /// re-resolves from the live tree. In <b>both</b> modes locator metadata is stored so a
        /// RuntimeId can always be mapped back to a live element.
        /// Set once at startup from configuration; thread-safe for reads after init.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Resolves a RuntimeId to a LIVE element.
        /// <para>
        /// Enabled: validates the persisted reference and re-finds it if stale.
        /// Disabled: re-resolves from the live tree on every access, falling back to the persisted
        /// reference only when a scoped re-find finds nothing (e.g. the desktop root, which is not a
        /// descendant of itself and so cannot be re-found by a scoped search).
        /// </para>
        /// Always returns a live <see cref="AutomationElement"/> or <c>false</c>.
        /// </summary>
        public static bool TryGetLive(string runtimeId, out AutomationElement element)
        {
            element = null;
            if (string.IsNullOrEmpty(runtimeId) || !_cache.TryGetValue(runtimeId, out var cached))
                return false;

            if (!Enabled)
            {
                // "Parse the live tree per request": re-resolve from the current tree first.
                var fresh = ReResolve(cached, runtimeId);
                if (fresh != null)
                {
                    cached.Element = fresh;
                    element = fresh;
                    return true;
                }

                // Re-find found nothing — fall back to the stored handle if it is still alive.
                if (IsLive(cached.Element))
                {
                    element = cached.Element;
                    return true;
                }

                _cache.TryRemove(runtimeId, out _);
                return false;
            }

            // Enabled: probe liveness via a fast COM call, re-find if the handle went stale.
            try
            {
                _ = cached.Element.Current.ProcessId;
                element = cached.Element;
                return true;
            }
            catch (ElementNotAvailableException)
            {
                Trace.WriteLine($"[ElementCache] Element '{runtimeId}' is stale (PID={cached.ProcessId}, Name='{cached.Name}'). Attempting re-find.");
                var refound = ReResolve(cached, runtimeId);
                if (refound != null)
                {
                    Trace.WriteLine($"[ElementCache] Re-found element '{runtimeId}' successfully.");
                    cached.Element = refound;
                    element = refound;
                    return true;
                }

                Trace.WriteLine($"[ElementCache] Failed to re-find element '{runtimeId}'. Removing from cache.");
                _cache.TryRemove(runtimeId, out _);
                return false;
            }
        }

        /// <summary>
        /// Registers an element with its locator info for future resolution and returns its RuntimeId.
        /// Locator metadata is stored in both enabled and disabled modes so the returned RuntimeId
        /// remains resolvable on subsequent calls.
        /// </summary>
        public static string CacheElement(AutomationElement element)
        {
            string runtimeId = string.Join(",", element.GetRuntimeId());

            try
            {
                _cache[runtimeId] = new CachedElement
                {
                    Element = element,
                    AutomationId = element.Current.AutomationId ?? "",
                    Name = element.Current.Name ?? "",
                    ClassName = element.Current.ClassName ?? "",
                    ControlTypeName = element.Current.ControlType.ProgrammaticName,
                    ProcessId = element.Current.ProcessId
                };
            }
            catch (ElementNotAvailableException)
            {
                // Element died between discovery and caching — skip
            }
            return runtimeId;
        }

        /// <summary>
        /// Registers an element using locator values the caller has ALREADY read (e.g. from a
        /// <see cref="System.Windows.Automation.CacheRequest"/> batch), avoiding the per-property
        /// COM round-trips that the <see cref="CacheElement(AutomationElement)"/> overload incurs.
        /// Stores metadata in both modes. Returns the supplied <paramref name="runtimeId"/> unchanged.
        /// </summary>
        public static string CacheElement(
            AutomationElement element,
            string runtimeId,
            string automationId,
            string name,
            string className,
            string controlTypeName,
            int processId)
        {
            if (string.IsNullOrEmpty(runtimeId)) return runtimeId;

            _cache[runtimeId] = new CachedElement
            {
                Element = element,
                AutomationId = automationId ?? "",
                Name = name ?? "",
                ClassName = className ?? "",
                ControlTypeName = controlTypeName ?? "",
                ProcessId = processId
            };
            return runtimeId;
        }

        /// <summary>
        /// Removes all registered elements belonging to a specific process.
        /// </summary>
        public static int ClearByProcess(int processId)
        {
            int removed = 0;
            foreach (var kvp in _cache)
            {
                if (kvp.Value.ProcessId == processId)
                {
                    if (_cache.TryRemove(kvp.Key, out _))
                        removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// Removes all registered elements belonging to processes with the given name.
        /// Works like CloseApp — resolves process name to PIDs.
        /// </summary>
        public static int ClearByName(string appName)
        {
            if (string.IsNullOrEmpty(appName)) return 0;

            string name = StripExeExtension(appName);

            var pids = new HashSet<int>(
                Process.GetProcessesByName(name).Select(p => p.Id));

            int removed = 0;
            foreach (var kvp in _cache)
            {
                if (pids.Contains(kvp.Value.ProcessId))
                {
                    if (_cache.TryRemove(kvp.Key, out _))
                        removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// Clears all registered elements.
        /// </summary>
        public static void Clear() => _cache.Clear();

        // ---------------------------------------------------------------- dead-process sweep

        private static System.Threading.Timer? _sweeper;

        /// <summary>
        /// Starts a periodic background sweep that evicts cache entries (and structure snapshots)
        /// belonging to processes that have exited. Without it, entries for dead apps linger until
        /// individually touched, growing the cache unbounded in a long-running server.
        /// Idempotent; called once at server startup. Not started implicitly so tests using fake
        /// PIDs are unaffected.
        /// </summary>
        public static void StartSweeper(TimeSpan interval)
        {
            _sweeper ??= new System.Threading.Timer(
                _ => { try { SweepDeadProcesses(); } catch (Exception ex) { Trace.WriteLine($"[ElementCache] Sweep failed: {ex.Message}"); } },
                state: null, dueTime: interval, period: interval);
        }

        /// <summary>
        /// Removes all entries whose owning process is no longer running.
        /// Returns the number of removed elements.
        /// </summary>
        public static int SweepDeadProcesses()
        {
            var pids = new HashSet<int>();
            foreach (var entry in _cache.Values)
                pids.Add(entry.ProcessId);

            int removed = 0;
            foreach (var pid in pids)
            {
                if (IsProcessAlive(pid)) continue;
                removed += ClearByProcess(pid);
                StructureSnapshotStore.ClearByProcess(pid);
            }
            return removed;
        }

        private static bool IsProcessAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                using var p = Process.GetProcessById(pid);
                return !p.HasExited;
            }
            catch
            {
                return false; // not found / access issues — treat as gone
            }
        }

        /// <summary>
        /// Number of registered elements.
        /// </summary>
        public static int Count => _cache.Count;

        /// <summary>
        /// Re-resolves a live element from the current UI tree using the stored locator metadata.
        /// Tries the locator-scoped re-find first (cheap, PID-filtered) and falls back to a
        /// RuntimeId comparison walk scoped to the owning process for elements whose locator
        /// properties are all empty (anonymous containers).
        /// </summary>
        private static AutomationElement ReResolve(CachedElement cached, string runtimeId)
        {
            return TryRefind(cached, runtimeId) ?? FindByRuntimeId(cached.ProcessId, runtimeId);
        }

        /// <summary>
        /// Attempts to re-find an element using its stored locator properties (AutomationId/Name/
        /// ClassName), searching only under the owning process's top-level windows — a desktop-wide
        /// <c>TreeScope.Descendants</c> walk forces UIA to visit every other app's tree and is the
        /// most expensive query it has. When several elements share the same locator the result is
        /// disambiguated by RuntimeId. Returns null if nothing matching is currently live.
        /// </summary>
        private static AutomationElement TryRefind(CachedElement cached, string runtimeId)
        {
            try
            {
                var conditions = new List<Condition>();

                if (!string.IsNullOrEmpty(cached.AutomationId))
                    conditions.Add(new PropertyCondition(
                        AutomationElement.AutomationIdProperty, cached.AutomationId));

                if (!string.IsNullOrEmpty(cached.Name))
                    conditions.Add(new PropertyCondition(
                        AutomationElement.NameProperty, cached.Name));

                if (!string.IsNullOrEmpty(cached.ClassName))
                    conditions.Add(new PropertyCondition(
                        AutomationElement.ClassNameProperty, cached.ClassName));

                // No locator at all (anonymous container) — the RuntimeId walk handles those.
                if (conditions.Count == 0) return null;

                Condition condition = conditions.Count == 1
                    ? conditions[0]
                    : new AndCondition(conditions.ToArray());

                var matches = FindInProcess(cached.ProcessId, condition);

                if (matches.Count == 0) return null;
                if (matches.Count == 1) return matches[0];

                // Multiple locator matches — prefer the one whose RuntimeId still matches.
                if (!string.IsNullOrEmpty(runtimeId))
                {
                    foreach (var m in matches)
                    {
                        try
                        {
                            if (string.Join(",", m.GetRuntimeId()) == runtimeId)
                                return m;
                        }
                        catch (ElementNotAvailableException) { }
                    }
                }

                return matches[0];
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[ElementCache] TryRefind failed for PID={cached.ProcessId}, Name='{cached.Name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Last-resort resolution: walk the owning process's element trees and return the element
        /// whose RuntimeId matches. A <see cref="PropertyCondition"/> on RuntimeId is not reliably
        /// supported by UIA, so we walk the process subtrees and compare RuntimeIds.
        /// </summary>
        private static AutomationElement FindByRuntimeId(int processId, string runtimeId)
        {
            if (processId <= 0 || string.IsNullOrEmpty(runtimeId)) return null;

            try
            {
                foreach (var e in FindInProcess(processId, Condition.TrueCondition))
                {
                    try
                    {
                        if (string.Join(",", e.GetRuntimeId()) == runtimeId)
                            return e;
                    }
                    catch (ElementNotAvailableException) { }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[ElementCache] FindByRuntimeId failed for PID={processId}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Finds elements matching <paramref name="condition"/> under the process's top-level
        /// windows only: one cheap PID-filtered scan of the desktop's direct children, then
        /// subtree searches scoped to those windows. Never walks other applications' trees.
        /// </summary>
        private static List<AutomationElement> FindInProcess(int processId, Condition condition)
        {
            var matches = new List<AutomationElement>();

            AutomationElementCollection topLevels;
            try
            {
                var pidCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
                topLevels = AutomationElement.RootElement.FindAll(TreeScope.Children, pidCondition);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[ElementCache] Top-level window scan failed for PID={processId}: {ex.Message}");
                return matches;
            }

            foreach (AutomationElement top in topLevels)
            {
                try
                {
                    foreach (AutomationElement m in top.FindAll(TreeScope.Subtree, condition))
                        matches.Add(m);
                }
                catch (ElementNotAvailableException) { /* window closed mid-search */ }
            }

            return matches;
        }

        /// <summary>
        /// Fast liveness probe for a persisted handle. Returns false for a null or dead element.
        /// </summary>
        private static bool IsLive(AutomationElement element)
        {
            if (element == null) return false;
            try
            {
                _ = element.Current.ProcessId;
                return true;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
        }

        /// <summary>
        /// Strips .exe extension from a name if present. Shared utility to avoid duplication.
        /// </summary>
        public static string StripExeExtension(string name)
        {
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(name);
            return name;
        }
    }
}
