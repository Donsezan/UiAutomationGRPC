namespace UiAutomationGRPC.Server.Models
{
    /// <summary>
    /// Configuration for SendKeys input filtering.
    /// Follows the same WhiteList / BlackList pattern as <see cref="AppAccessConfig"/>.
    /// When both lists are empty, all key input is allowed.
    /// </summary>
    public class KeyRestrictionConfig
    {
        /// <summary>
        /// When non-empty, only key inputs matching an entry are permitted.
        /// Supports the special <c>{PLAINTEXT}</c> token to allow any input
        /// that contains no SendKeys modifiers or special key codes.
        /// </summary>
        public List<string> WhiteList { get; set; } = new();

        /// <summary>
        /// Key patterns to block. Uses substring containment matching
        /// so embedded patterns (e.g. "abc%{F4}xyz") are also caught.
        /// </summary>
        public List<string> BlackList { get; set; } = new();
    }
}
