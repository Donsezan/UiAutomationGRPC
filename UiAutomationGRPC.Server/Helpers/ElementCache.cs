using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using Trace = System.Diagnostics.Trace;
using PropertyCondition = System.Windows.Automation.PropertyCondition;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Thread-safe element cache that validates liveness on every access.
    /// Stores locator info alongside references so dead elements can be re-found.
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
        /// Controls whether element caching is active.
        /// When false, every cache read/write becomes a no-op, forcing callers to
        /// re-resolve elements from the live UI Automation tree on every request.
        /// Set once at startup from configuration; thread-safe for reads after init.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets a LIVE element: validates the cached ref, re-finds if stale.
        /// Always returns a live AutomationElement or false.
        /// Returns false immediately when caching is disabled.
        /// </summary>
        public static bool TryGetLive(string runtimeId, out AutomationElement element)
        {
            element = null;
            if (!Enabled || !_cache.TryGetValue(runtimeId, out var cached)) return false;

            // Probe liveness via a fast COM call
            try
            {
                _ = cached.Element.Current.ProcessId;
                element = cached.Element;
                return true;
            }
            catch (ElementNotAvailableException)
            {
                // Element is dead — try re-finding by locator
                Trace.WriteLine($"[ElementCache] Element '{runtimeId}' is stale (PID={cached.ProcessId}, Name='{cached.Name}'). Attempting re-find.");
                var refound = TryRefind(cached);
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
        /// Caches an element with its locator info for future re-finding.
        /// When caching is disabled, the RuntimeId is still derived and returned
        /// (callers depend on it for response contracts) but the element is not stored.
        /// </summary>
        public static string CacheElement(AutomationElement element)
        {
            string runtimeId = string.Join(",", element.GetRuntimeId());
            if (!Enabled) return runtimeId;

            try
            {
                var cached = new CachedElement
                {
                    Element = element,
                    AutomationId = element.Current.AutomationId ?? "",
                    Name = element.Current.Name ?? "",
                    ClassName = element.Current.ClassName ?? "",
                    ControlTypeName = element.Current.ControlType.ProgrammaticName,
                    ProcessId = element.Current.ProcessId
                };
                _cache[runtimeId] = cached;
            }
            catch (ElementNotAvailableException)
            {
                // Element died between discovery and caching — skip
            }
            return runtimeId;
        }

        /// <summary>
        /// Removes all cached elements belonging to a specific process.
        /// Returns 0 immediately when caching is disabled.
        /// </summary>
        public static int ClearByProcess(int processId)
        {
            if (!Enabled) return 0;
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
        /// Removes all cached elements belonging to processes with the given name.
        /// Works like CloseApp — resolves process name to PIDs.
        /// Returns 0 immediately when caching is disabled.
        /// </summary>
        public static int ClearByName(string appName)
        {
            if (!Enabled || string.IsNullOrEmpty(appName)) return 0;

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
        /// Clears all cached elements.
        /// </summary>
        public static void Clear() => _cache.Clear();

        /// <summary>
        /// Number of cached elements. Returns 0 when caching is disabled.
        /// </summary>
        public static int Count => Enabled ? _cache.Count : 0;

        /// <summary>
        /// Attempts to re-find a dead element using its stored locator properties.
        /// Searches the entire desktop tree within the same process.
        /// </summary>
        private static AutomationElement TryRefind(CachedElement cached)
        {
            try
            {
                var conditions = new List<Condition>
                {
                    new PropertyCondition(AutomationElement.ProcessIdProperty, cached.ProcessId)
                };

                if (!string.IsNullOrEmpty(cached.AutomationId))
                    conditions.Add(new PropertyCondition(
                        AutomationElement.AutomationIdProperty, cached.AutomationId));

                if (!string.IsNullOrEmpty(cached.Name))
                    conditions.Add(new PropertyCondition(
                        AutomationElement.NameProperty, cached.Name));

                if (!string.IsNullOrEmpty(cached.ClassName))
                    conditions.Add(new PropertyCondition(
                        AutomationElement.ClassNameProperty, cached.ClassName));

                if (conditions.Count < 2) return null;

                var condition = new AndCondition(conditions.ToArray());
                return AutomationElement.RootElement.FindFirst(
                    TreeScope.Descendants, condition);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[ElementCache] TryRefind failed for PID={cached.ProcessId}, Name='{cached.Name}': {ex.Message}");
                return null;
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
