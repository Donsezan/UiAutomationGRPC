namespace UiAutomationGRPC.Server.Models
{
    /// <summary>
    /// A whitelisted application entry. Only these apps may be launched when the whitelist is non-empty.
    /// </summary>
    public class WhiteListEntry
    {
        public string Path { get; set; } = "";
        public List<string> AllowedArgs { get; set; } = new();
    }

    /// <summary>
    /// A blacklisted application entry.
    /// When Path is empty, RestrictedArgs apply globally to all apps.
    /// </summary>
    public class BlackListEntry
    {
        public string Path { get; set; } = "";
        public List<string> RestrictedArgs { get; set; } = new();
    }

    /// <summary>
    /// Top-level configuration for application access control.
    /// Controls both app launch restrictions (via AppAccessValidator) and
    /// element interaction restrictions (via InteractionAccessGuard).
    /// </summary>
    public class AppAccessConfig
    {
        public List<WhiteListEntry> WhiteList { get; set; } = new();
        public List<BlackListEntry> BlackList { get; set; } = new();

        /// <summary>
        /// When true and WhiteList/BlackList is configured, element interactions
        /// are also restricted to processes whose executable matches the list.
        /// Defaults to true so the whitelist governs both launch and interaction.
        /// </summary>
        public bool RestrictInteractions { get; set; } = true;
    }
}
