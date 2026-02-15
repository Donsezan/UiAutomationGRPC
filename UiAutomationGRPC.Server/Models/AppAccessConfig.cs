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
    /// </summary>
    public class AppAccessConfig
    {
        public List<WhiteListEntry> WhiteList { get; set; } = new();
        public List<BlackListEntry> BlackList { get; set; } = new();
    }
}
