using System.Collections.Concurrent;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Computes the change set between two app-structure trees so <c>diff_mode</c> requests can
    /// return "what changed since the last look" instead of the whole tree. For an action that
    /// flips one label in a 2000-node app this shrinks the response by orders of magnitude —
    /// the difference between a usable and an unusable loop on large dynamic UIs.
    ///
    /// Nodes are matched by <see cref="AppNode.UniqId"/> (RuntimeId). RuntimeIds are stable while
    /// an element stays alive; a recreated element shows up as removed + added, which is an
    /// accurate description of what the UI did.
    /// </summary>
    public static class StructureDiff
    {
        /// <summary>A flat diff entry: the node (without children) plus its parent's UniqId for orientation.</summary>
        public sealed class Entry
        {
            public string Parent { get; set; } = "";
            public AppNode Node { get; set; } = new();
        }

        public sealed class Result
        {
            /// <summary>Marker so a client/LLM can tell a diff payload from a full tree.</summary>
            public bool Diff => true;
            public List<Entry> Added { get; } = new();
            public List<Entry> Changed { get; } = new();
            public List<string> Removed { get; } = new();

            public bool IsEmpty => Added.Count == 0 && Changed.Count == 0 && Removed.Count == 0;
        }

        public static Result Compute(AppNode previous, AppNode current)
        {
            var result = new Result();
            var prevMap = Flatten(previous);
            var curMap = Flatten(current);

            foreach (var (id, cur) in curMap)
            {
                if (!prevMap.TryGetValue(id, out var prev))
                    result.Added.Add(new Entry { Parent = cur.Parent, Node = WithoutChildren(cur.Node) });
                else if (!NodesEqual(prev.Node, cur.Node) || prev.Parent != cur.Parent)
                    result.Changed.Add(new Entry { Parent = cur.Parent, Node = WithoutChildren(cur.Node) });
            }

            foreach (var id in prevMap.Keys)
            {
                if (!curMap.ContainsKey(id))
                    result.Removed.Add(id);
            }

            return result;
        }

        private static Dictionary<string, (AppNode Node, string Parent)> Flatten(AppNode root)
        {
            var map = new Dictionary<string, (AppNode, string)>();
            void Walk(AppNode node, string parent)
            {
                if (string.IsNullOrEmpty(node.UniqId)) return;
                map[node.UniqId] = (node, parent);
                foreach (var child in node.Children)
                    Walk(child, node.UniqId);
            }
            Walk(root, "");
            return map;
        }

        private static bool NodesEqual(AppNode a, AppNode b) =>
            a.UiAutomationId == b.UiAutomationId
            && a.Name == b.Name
            && a.ControlType == b.ControlType
            && a.BoundingRectangle == b.BoundingRectangle
            && a.IsClickable == b.IsClickable
            && a.IsVisible == b.IsVisible;

        private static AppNode WithoutChildren(AppNode n) => new()
        {
            UniqId = n.UniqId,
            UiAutomationId = n.UiAutomationId,
            Name = n.Name,
            Description = n.Description,
            ControlType = n.ControlType,
            BoundingRectangle = n.BoundingRectangle,
            IsClickable = n.IsClickable,
            IsVisible = n.IsVisible
        };
    }

    /// <summary>
    /// Holds the most recent app-structure tree per build root so diff_mode has a base to compare
    /// against. Keyed by the root element's RuntimeId (stable while the window lives) and tagged
    /// with the owning PID for scoped eviction alongside the element cache.
    /// </summary>
    public static class StructureSnapshotStore
    {
        private const int MaxEntries = 32; // safety net — a handful of windows in practice

        private static readonly ConcurrentDictionary<string, (int Pid, AppNode Tree)> _snapshots = new();

        public static AppNode? Get(string rootUniqId) =>
            _snapshots.TryGetValue(rootUniqId, out var entry) ? entry.Tree : null;

        public static void Set(string rootUniqId, int pid, AppNode tree)
        {
            if (string.IsNullOrEmpty(rootUniqId)) return;

            if (_snapshots.Count >= MaxEntries && !_snapshots.ContainsKey(rootUniqId))
                _snapshots.Clear(); // crude but safe: snapshots are an optimisation, not state

            _snapshots[rootUniqId] = (pid, tree);
        }

        public static void ClearByProcess(int pid)
        {
            foreach (var kvp in _snapshots)
            {
                if (kvp.Value.Pid == pid)
                    _snapshots.TryRemove(kvp.Key, out _);
            }
        }

        public static void Clear() => _snapshots.Clear();

        public static int Count => _snapshots.Count;
    }
}
