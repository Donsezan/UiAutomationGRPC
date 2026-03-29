using System.Text.RegularExpressions;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Validates whether a SendKeys input string is permitted based on
    /// WhiteList / BlackList configuration.
    /// Follows the same evaluation pattern as <see cref="AppAccessValidator"/>.
    /// </summary>
    public partial class KeyAccessValidator
    {
        private const string PlainTextToken = "{PLAINTEXT}";

        /// <summary>
        /// Matches any SendKeys special character: modifiers (+^%), tilde (~),
        /// grouping parentheses, and brace-wrapped key codes.
        /// </summary>
        [GeneratedRegex(@"[+^%~(){}]")]
        private static partial Regex SendKeysSpecialChars();

        private readonly KeyRestrictionConfig _config;
        private readonly bool _hasWhiteList;
        private readonly bool _hasBlackList;

        public KeyAccessValidator(KeyRestrictionConfig config)
        {
            _config = config ?? new KeyRestrictionConfig();
            _hasWhiteList = _config.WhiteList.Count > 0;
            _hasBlackList = _config.BlackList.Count > 0;
        }

        /// <summary>
        /// Validates the given SendKeys input against configured restrictions.
        /// </summary>
        public (bool Allowed, string Reason) Validate(string keys)
        {
            if (string.IsNullOrEmpty(keys))
                return (false, "Key input cannot be empty.");

            if (!_hasWhiteList && !_hasBlackList)
                return (true, "Allowed — no key restrictions configured.");

            if (_hasWhiteList && !IsWhiteListed(keys))
                return (false, $"Key input '{keys}' is not in the whitelist.");

            var blackMatch = FindBlackListMatch(keys);
            if (blackMatch is not null)
                return (false, $"Key input contains restricted pattern '{blackMatch}'.");

            return (true, _hasWhiteList ? "Allowed by whitelist." : "Allowed.");
        }

        // ────────────────────────────── Private helpers ──────────────────────────────

        private bool IsWhiteListed(string keys)
        {
            var whiteListHasPlainText = false;

            foreach (var entry in _config.WhiteList)
            {
                if (entry.Equals(PlainTextToken, StringComparison.OrdinalIgnoreCase))
                {
                    whiteListHasPlainText = true;
                    continue;
                }

                // Exact match against a specific whitelisted key sequence
                if (entry.Equals(keys, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // {PLAINTEXT} matches any input free of SendKeys special characters
            return whiteListHasPlainText && IsPlainText(keys);
        }

        private string? FindBlackListMatch(string keys) =>
            _config.BlackList.FirstOrDefault(pattern =>
                keys.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Returns <c>true</c> when the input contains no SendKeys modifier
        /// characters (+, ^, %, ~) and no brace-wrapped special key codes.
        /// </summary>
        internal static bool IsPlainText(string keys) =>
            !SendKeysSpecialChars().IsMatch(keys);
    }
}
