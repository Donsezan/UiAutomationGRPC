using System.Collections.Concurrent;
using System.Diagnostics;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Guards element interactions by validating whether the owning process
    /// is permitted by the configured WhiteList / BlackList.
    /// 
    /// Caches decisions by ProcessId so each PID is resolved and validated
    /// only once — subsequent lookups are O(1) dictionary reads.
    /// 
    /// Reuses <see cref="AppAccessValidator"/> for path matching to keep
    /// the launch and interaction policies consistent.
    /// </summary>
    public class InteractionAccessGuard
    {
        private readonly AppAccessValidator _validator;
        private readonly AppAccessConfig _config;
        private readonly ConcurrentDictionary<int, (bool Allowed, string Reason)> _cache = new();

        public InteractionAccessGuard(AppAccessValidator validator, AppAccessConfig config)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Returns true when the guard is actively enforcing restrictions.
        /// When false, all calls to <see cref="IsAllowed"/> short-circuit to allowed.
        /// </summary>
        public bool IsActivelyRestricting
        {
            get
            {
                if (!_config.RestrictInteractions)
                    return false;

                var hasWhiteList = _config.WhiteList.Any(w => !string.IsNullOrWhiteSpace(w.Path));
                var hasBlackList = _config.BlackList.Any(b => !string.IsNullOrWhiteSpace(b.Path));

                return hasWhiteList || hasBlackList;
            }
        }

        /// <summary>
        /// Validates whether the process identified by <paramref name="processId"/>
        /// is allowed to be interacted with.
        /// Returns cached result on subsequent calls for the same PID.
        /// </summary>
        public (bool Allowed, string Reason) IsAllowed(int processId)
        {
            if (!IsActivelyRestricting)
                return (true, "No interaction restrictions configured.");

            return _cache.GetOrAdd(processId, pid => ResolveAndValidate(pid));
        }

        /// <summary>
        /// Convenience method for handler guard checks.
        /// Returns null when the process is allowed, or a formatted block
        /// reason string when denied. Handles null guard references via
        /// the static helper <see cref="CheckAccess"/>.
        /// </summary>
        public string? GetBlockReason(int processId)
        {
            var (allowed, reason) = IsAllowed(processId);
            return allowed ? null : $"Interaction blocked: {reason}";
        }

        /// <summary>
        /// Static helper for nullable guard references.
        /// Returns null (allowed) when guard is null or process is permitted,
        /// or a formatted block message when denied.
        /// </summary>
        public static string? CheckAccess(InteractionAccessGuard? guard, int processId)
        {
            return guard?.GetBlockReason(processId);
        }

        /// <summary>
        /// Resolves the process executable path and delegates to AppAccessValidator.
        /// This is called once per PID and the result is cached.
        /// </summary>
        private (bool Allowed, string Reason) ResolveAndValidate(int processId)
        {
            string processPath;
            try
            {
                var process = Process.GetProcessById(processId);
                processPath = process.MainModule?.FileName
                    ?? throw new InvalidOperationException($"Cannot determine executable path for PID {processId}.");
            }
            catch (ArgumentException)
            {
                // Process has already exited
                return (false, $"Process {processId} is no longer running.");
            }
            catch (InvalidOperationException ex)
            {
                return (false, $"Cannot resolve process {processId}: {ex.Message}");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Access denied — cannot read process module info
                return (false, $"Access denied when inspecting process {processId}: {ex.Message}");
            }

            // Delegate to AppAccessValidator — pass null for arguments since
            // interaction checks don't involve launch arguments
            var (allowed, _, reason) = _validator.Validate(processPath, arguments: null);
            return (allowed, reason);
        }
    }
}
