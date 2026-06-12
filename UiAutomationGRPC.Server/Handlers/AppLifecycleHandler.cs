using System.Diagnostics;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Handlers
{
    /// <summary>
    /// Handles application lifecycle operations.
    /// </summary>
    public class AppLifecycleHandler
    {
        private const int ProcessExitTimeoutMs = 5000;

        private readonly AppAccessValidator? _validator;
        private readonly InteractionAccessGuard? _guard;
        private readonly ILogger<AppLifecycleHandler> _logger;

        public AppLifecycleHandler(ILogger<AppLifecycleHandler> logger, AppAccessValidator? validator = null, InteractionAccessGuard? guard = null)
        {
            _logger = logger;
            _validator = validator;
            _guard = guard;
        }

        /// <summary>
        /// Launches an application and returns its process ID.
        /// </summary>
        /// <remarks>
        /// <para><b>UWP / Store apps:</b> for packaged apps launched via an alias (e.g. <c>calc</c>),
        /// Windows starts the app through a host/launcher process whose PID exits almost
        /// immediately. When that happens the handler resolves the PID of the newly-appeared
        /// top-level window via <see cref="UwpPidResolver"/> and returns that instead, so the
        /// returned <c>ProcessId</c> works with PID-addressed RPCs for both Win32 and UWP apps.
        /// Resolution is best-effort: if no new window appears within the budget, the launcher
        /// PID is returned with a warning in the message.</para>
        /// </remarks>
        public Task<OpenAppResponse> OpenApp(AppRequest request, ServerCallContext context)
        {
            _logger.LogInformation("OpenApp requested: AppName='{AppName}', Arguments='{Arguments}'",
                request.AppName, request.Arguments ?? "");

            try
            {
                string fileName = request.AppName;

                // Validate against whitelist / blacklist if configured
                if (_validator != null)
                {
                    var (allowed, resolvedPath, reason) = _validator.Validate(request.AppName, request.Arguments);
                    if (!allowed)
                    {
                        _logger.LogWarning("OpenApp BLOCKED: AppName='{AppName}', Reason='{Reason}'",
                            request.AppName, reason);
                        return Task.FromResult(new OpenAppResponse { Success = false, Message = $"Blocked: {reason}" });
                    }

                    _logger.LogInformation("OpenApp validated: AppName='{AppName}' resolved to '{ResolvedPath}'",
                        request.AppName, resolvedPath);
                    fileName = resolvedPath;
                }

                // Snapshot visible top-level windows BEFORE launching so a UWP launcher's real
                // window can be identified by diff if the launcher exits (see UwpPidResolver).
                var windowsBefore = UwpPidResolver.VisibleTopLevelWindows();

                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = request.Arguments ?? "",
                    UseShellExecute = true
                };
                var process = Process.Start(startInfo);

                var (pid, resolved, launcherExited) = UwpPidResolver.ResolveLaunchedPid(process, windowsBefore);
                string message = resolved
                    ? "App started (launcher exited; process_id resolved to the new window's owner)"
                    : launcherExited
                        ? "App started, but the launched process already exited and no new window was found — " +
                          "the returned process_id may be stale. Address the app by name, or use WaitForElement."
                        : "App started";

                _logger.LogInformation("OpenApp succeeded: AppName='{AppName}', PID={ProcessId} (resolved={Resolved})",
                    request.AppName, pid, resolved);
                return Task.FromResult(new OpenAppResponse { Success = true, Message = message, ProcessId = pid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenApp FAILED: AppName='{AppName}'", request.AppName);
                return Task.FromResult(new OpenAppResponse { Success = false, Message = $"Failed to start app: {ex.Message}" });
            }
        }

        public Task<PerformActionResponse> CloseApp(AppRequest request, ServerCallContext context)
        {
            _logger.LogInformation("CloseApp requested: AppName='{AppName}'", request.AppName);

            try
            {
                // GetProcessesByName expects a name without extension — strip ".exe"
                // so callers can pass either "notepad" or "notepad.exe" consistently
                // with GetAppStructure / ClearByName.
                string processName = ElementCache.StripExeExtension(request.AppName);
                var processes = Process.GetProcessesByName(processName);
                _logger.LogInformation("CloseApp: Found {Count} process(es) for '{AppName}'",
                    processes.Length, request.AppName);

                int killed = 0;
                var exceptions = new List<string>();

                foreach (var p in processes)
                {
                    try
                    {
                        int pid = p.Id;

                        // Respect interaction restrictions — terminating a process is an
                        // interaction and must obey the same WhiteList / BlackList policy.
                        var blocked = InteractionAccessGuard.CheckAccess(_guard, pid);
                        if (blocked != null)
                        {
                            _logger.LogWarning("CloseApp: BLOCKED process {ProcessId}: {Reason}", pid, blocked);
                            exceptions.Add($"PID {pid}: {blocked}");
                            continue;
                        }

                        p.Kill();
                        if (!p.WaitForExit(ProcessExitTimeoutMs))
                        {
                            _logger.LogWarning("CloseApp: Process {ProcessId} did not exit within {Timeout}ms timeout",
                                pid, ProcessExitTimeoutMs);
                        }
                        killed++;
                        _logger.LogInformation("CloseApp: Killed process {ProcessId}", pid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "CloseApp: Failed to kill process {ProcessId}", p.Id);
                        exceptions.Add($"PID {p.Id}: {ex.Message}");
                    }
                }

                if (exceptions.Count > 0)
                {
                    string message = $"Closed {killed}/{processes.Length} instance(s). Failures: {string.Join("; ", exceptions)}";
                    _logger.LogWarning("CloseApp partial failure: {Message}", message);
                    return Task.FromResult(new PerformActionResponse
                    {
                        Success = killed > 0,
                        Message = message
                    });
                }

                return Task.FromResult(new PerformActionResponse
                {
                    Success = true,
                    Message = $"All instances of app closed. Instance count: {processes.Length}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CloseApp FAILED: AppName='{AppName}'", request.AppName);
                return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Failed to close app: {ex.Message}" });
            }
        }

        public Task<PerformActionResponse> CloseAppByProcessId(CloseAppByProcessIdRequest request, ServerCallContext context)
        {
            _logger.LogInformation("CloseAppByProcessId requested: PID={ProcessId}", request.ProcessId);

            try
            {
                // Respect interaction restrictions — terminating a process must obey
                // the same WhiteList / BlackList policy as other interactions.
                var blocked = InteractionAccessGuard.CheckAccess(_guard, request.ProcessId);
                if (blocked != null)
                {
                    _logger.LogWarning("CloseAppByProcessId: BLOCKED PID={ProcessId}: {Reason}", request.ProcessId, blocked);
                    return Task.FromResult(new PerformActionResponse { Success = false, Message = blocked });
                }

                var process = Process.GetProcessById(request.ProcessId);
                process.Kill();
                if (!process.WaitForExit(ProcessExitTimeoutMs))
                {
                    _logger.LogWarning("CloseAppByProcessId: Process {ProcessId} did not exit within {Timeout}ms timeout",
                        request.ProcessId, ProcessExitTimeoutMs);
                }

                _logger.LogInformation("CloseAppByProcessId succeeded: PID={ProcessId}", request.ProcessId);
                return Task.FromResult(new PerformActionResponse { Success = true, Message = $"Process {request.ProcessId} closed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CloseAppByProcessId FAILED: PID={ProcessId}", request.ProcessId);
                return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Failed to close process {request.ProcessId}: {ex.Message}" });
            }
        }
    }
}
