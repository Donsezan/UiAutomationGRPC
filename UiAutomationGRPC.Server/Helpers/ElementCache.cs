using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
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
        /// Gets a LIVE element: validates the cached ref, re-finds if stale.
        /// Always returns a live AutomationElement or false.
        /// </summary>
        public static bool TryGetLive(string runtimeId, out AutomationElement element)
        {
            element = null;
            if (!_cache.TryGetValue(runtimeId, out var cached)) return false;

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
                var refound = TryRefind(cached);
                if (refound != null)
                {
                    cached.Element = refound;
                    element = refound;
                    return true;
                }

                _cache.TryRemove(runtimeId, out _);
                return false;
            }
        }

        /// <summary>
        /// Caches an element with its locator info for future re-finding.
        /// </summary>
        public static string CacheElement(AutomationElement element)
        {
            string runtimeId = string.Join(",", element.GetRuntimeId());
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
        /// Removes all cached elements belonging to processes with the given name.
        /// Works like CloseApp — resolves process name to PIDs.
        /// </summary>
        public static int ClearByName(string appName)
        {
            if (string.IsNullOrEmpty(appName)) return 0;

            string name = appName;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = Path.GetFileNameWithoutExtension(name);

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
        /// Number of cached elements.
        /// </summary>
        public static int Count => _cache.Count;

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
            catch
            {
                return null;
            }
        }
    }
}
