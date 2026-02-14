using System.Diagnostics;
using System.IO;
using UiAutomationGRPC.Server.Models;

namespace UiAutomationGRPC.Server.Helpers
{
    /// <summary>
    /// Validates whether an application is allowed to launch based on WhiteList / BlackList configuration.
    /// </summary>
    public class AppAccessValidator
    {
        private readonly AppAccessConfig _config;

        public AppAccessValidator(AppAccessConfig config)
        {
            _config = config ?? new AppAccessConfig();
        }

        /// <summary>
        /// Validates launch request. Returns the resolved absolute path on success.
        /// </summary>
        public (bool Allowed, string ResolvedPath, string Reason) Validate(string appName, string? arguments)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return (false, "", "Application name cannot be empty.");

            // Block relative paths with traversal
            if (ContainsPathTraversal(appName))
                return (false, "", $"Relative path traversal is not allowed: '{appName}'.");

            // Resolve to an absolute path
            var resolvedPath = ResolveToAbsolutePath(appName);
            if (resolvedPath == null)
                return (false, "", $"Could not resolve application to an absolute path: '{appName}'.");

            // Parse arguments into individual tokens for comparison
            var argTokens = ParseArguments(arguments);

            var hasWhiteList = _config.WhiteList.Any(w => !string.IsNullOrWhiteSpace(w.Path));
            var globalBlackListArgs = GetGlobalRestrictedArgs();

            if (hasWhiteList)
                return ValidateWithWhiteList(resolvedPath, argTokens, globalBlackListArgs);
            else
                return ValidateWithoutWhiteList(resolvedPath, argTokens, globalBlackListArgs);
        }

        // ────────────────────────────── Private helpers ──────────────────────────────

        private (bool, string, string) ValidateWithWhiteList(string resolvedPath, List<string> argTokens, HashSet<string> globalBlackListArgs)
        {
            var whiteEntry = _config.WhiteList
                .FirstOrDefault(w => !string.IsNullOrWhiteSpace(w.Path) &&
                    PathEquals(w.Path, resolvedPath));

            if (whiteEntry == null)
                return (false, "", $"Application is not in the whitelist: '{resolvedPath}'.");

            // If whitelist entry specifies allowed args, enforce them
            if (whiteEntry.AllowedArgs.Count > 0)
            {
                foreach (var arg in argTokens)
                {
                    if (!whiteEntry.AllowedArgs.Any(a => string.Equals(a, arg, StringComparison.OrdinalIgnoreCase)))
                        return (false, resolvedPath, $"Argument '{arg}' is not in the allowed arguments for '{resolvedPath}'.");
                }
            }

            // Check global restricted args from blacklist (empty-path entries)
            foreach (var arg in argTokens)
            {
                if (globalBlackListArgs.Contains(arg.ToLowerInvariant()))
                    return (false, resolvedPath, $"Argument '{arg}' is globally restricted.");
            }

            return (true, resolvedPath, "Allowed by whitelist.");
        }

        private (bool, string, string) ValidateWithoutWhiteList(string resolvedPath, List<string> argTokens, HashSet<string> globalBlackListArgs)
        {
            // Check if the app is explicitly blacklisted (by path)
            var blackEntry = _config.BlackList
                .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Path) &&
                    PathEquals(b.Path, resolvedPath));

            if (blackEntry != null)
            {
                // If blacklist entry has restricted args, block only those args
                if (blackEntry.RestrictedArgs.Count > 0)
                {
                    foreach (var arg in argTokens)
                    {
                        if (blackEntry.RestrictedArgs.Any(r => string.Equals(r, arg, StringComparison.OrdinalIgnoreCase)))
                            return (false, resolvedPath, $"Argument '{arg}' is restricted for '{resolvedPath}'.");
                    }
                }
                else
                {
                    // No specific args → the entire app is blacklisted
                    return (false, resolvedPath, $"Application is blacklisted: '{resolvedPath}'.");
                }
            }

            // Check global restricted args (empty-path blacklist entries)
            foreach (var arg in argTokens)
            {
                if (globalBlackListArgs.Contains(arg.ToLowerInvariant()))
                    return (false, resolvedPath, $"Argument '{arg}' is globally restricted.");
            }

            return (true, resolvedPath, "Allowed.");
        }

        private HashSet<string> GetGlobalRestrictedArgs()
        {
            var args = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _config.BlackList.Where(b => string.IsNullOrWhiteSpace(b.Path)))
            {
                foreach (var arg in entry.RestrictedArgs)
                    args.Add(arg.ToLowerInvariant());
            }
            return args;
        }

        // ────────────────────────────── Static utilities ──────────────────────────────

        internal static bool ContainsPathTraversal(string path)
        {
            // Normalize separators and check for ".." segments
            var normalized = path.Replace('/', '\\');
            var segments = normalized.Split('\\');
            return segments.Any(s => s == "..");
        }

        /// <summary>
        /// Resolves a name/path to an absolute path. Tries:
        /// 1. If already an absolute path and exists → return it.
        /// 2. Combine with current directory if relative → return if exists.
        /// 3. Search PATH via where.exe.
        /// </summary>
        internal static string? ResolveToAbsolutePath(string appName)
        {
            try
            {
                // If the name is already an absolute path
                if (Path.IsPathRooted(appName))
                {
                    var full = Path.GetFullPath(appName);
                    if (File.Exists(full))
                        return full;
                    return null;
                }

                // Try current directory
                var localPath = Path.GetFullPath(appName);
                if (File.Exists(localPath))
                    return localPath;

                // Search system PATH using where.exe
                var resolved = SearchPath(appName);
                return resolved;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[AppAccessValidator] ResolveToAbsolutePath failed for '{appName}': {ex.Message}");
                return null;
            }
        }

        private static string? SearchPath(string appName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = appName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return null;

                var output = proc.StandardOutput.ReadLine(); // first match
                proc.WaitForExit(3000);
                if (!string.IsNullOrWhiteSpace(output) && File.Exists(output.Trim()))
                    return Path.GetFullPath(output.Trim());
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Trace.WriteLine($"[AppAccessValidator] SearchPath failed for '{appName}': {ex.Message}");
            }
            return null;
        }

        internal static List<string> ParseArguments(string? arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return new List<string>();

            // Split on whitespace, respecting quoted strings
            var tokens = new List<string>();
            var current = "";
            var inQuote = false;

            foreach (var ch in arguments)
            {
                if (ch == '"')
                {
                    inQuote = !inQuote;
                }
                else if (ch == ' ' && !inQuote)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += ch;
                }
            }
            if (current.Length > 0)
                tokens.Add(current);

            return tokens;
        }

        private static bool PathEquals(string a, string b)
        {
            try
            {
                var normA = Path.GetFullPath(a).TrimEnd('\\');
                var normB = Path.GetFullPath(b).TrimEnd('\\');
                return string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
