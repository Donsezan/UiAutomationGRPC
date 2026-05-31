namespace UiAutomationGRPC.Server.Models
{
    /// <summary>
    /// Tuning knobs for the LLM-facing app-structure tree
    /// (<c>GetAppStructure</c> / <c>PerformActionWithStructure</c>).
    /// Bound from the <c>Features:AppStructure</c> configuration section in <c>appsettings.json</c>.
    /// </summary>
    public class AppStructureOptions
    {
        /// <summary>
        /// Maximum tree depth to emit (root window = depth 0). 0 = unlimited.
        /// Nodes deeper than this are dropped and their parent is flagged
        /// <c>ChildrenTruncated</c>.
        /// </summary>
        public int MaxDepth { get; set; } = 40;

        /// <summary>
        /// Maximum total number of nodes to emit before truncating the walk. 0 = unlimited.
        /// Guards against thousands-of-element apps blowing up latency and token count.
        /// </summary>
        public int MaxNodes { get; set; } = 2000;

        /// <summary>
        /// When false (default) offscreen elements (and elements with a zero-size bounding
        /// rectangle) and their subtrees are skipped. The root window is never filtered.
        /// </summary>
        public bool IncludeOffscreen { get; set; } = false;

        /// <summary>
        /// When true (default) the JSON is emitted without indentation to save tokens on
        /// the LLM path. Set false for human-readable indented output.
        /// </summary>
        public bool CompactJson { get; set; } = true;
    }
}
