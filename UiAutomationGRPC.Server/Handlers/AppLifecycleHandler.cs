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
        private readonly ILogger<AppLifecycleHandler> _logger;

        public AppLifecycleHandler(ILogger<AppLifecycleHandler> logger, AppAccessValidator? validator = null)
        {
            _logger = logger;
            _validator = validator;
        }

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

                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = request.Arguments ?? "",
                    UseShellExecute = true
                };
                var process = Process.Start(startInfo);
                int pid = process?.Id ?? 0;

                _logger.LogInformation("OpenApp succeeded: AppName='{AppName}', PID={ProcessId}", request.AppName, pid);
                return Task.FromResult(new OpenAppResponse { Success = true, Message = "App started", ProcessId = pid });
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
                var processes = Process.GetProcessesByName(request.AppName);
                _logger.LogInformation("CloseApp: Found {Count} process(es) for '{AppName}'",
                    processes.Length, request.AppName);

                int killed = 0;
                var exceptions = new List<string>();

                foreach (var p in processes)
                {
                    try
                    {
                        int pid = p.Id;
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
