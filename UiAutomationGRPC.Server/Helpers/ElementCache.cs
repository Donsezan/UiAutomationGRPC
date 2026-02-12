using System.Collections.Concurrent;
using System.Windows.Automation;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Thread-safe cache for storing AutomationElement instances by RuntimeId.
    /// Shared across all handlers for element lookup.
    /// </summary>
    public static class ElementCache
    {
        private static readonly ConcurrentDictionary<string, AutomationElement> _cache = new();

        /// <summary>
        /// Attempts to get an element from the cache.
        /// </summary>
        public static bool TryGet(string runtimeId, out AutomationElement element)
        {
            return _cache.TryGetValue(runtimeId, out element);
        }

        /// <summary>
        /// Adds or updates an element in the cache.
        /// </summary>
        public static void AddOrUpdate(string runtimeId, AutomationElement element)
        {
            _cache[runtimeId] = element;
        }

        /// <summary>
        /// Attempts to add an element to the cache if not already present.
        /// </summary>
        public static bool TryAdd(string runtimeId, AutomationElement element)
        {
            return _cache.TryAdd(runtimeId, element);
        }

        /// <summary>
        /// Generates a string RuntimeId from an element and caches it.
        /// </summary>
        public static string CacheElement(AutomationElement element)
        {
            string runtimeId = string.Join(",", element.GetRuntimeId());
            TryAdd(runtimeId, element);
            return runtimeId;
        }

        /// <summary>
        /// Clears all cached elements.
        /// </summary>
        public static void Clear() => _cache.Clear();

        /// <summary>
        /// Number of cached elements.
        /// </summary>
        public static int Count => _cache.Count;
    }
}
