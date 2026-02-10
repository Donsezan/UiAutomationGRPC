using System.Diagnostics;
using Grpc.Core;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Handlers
{
    /// <summary>
    /// Handles application lifecycle operations.
    /// </summary>
    public class AppLifecycleHandler
    {
        public Task<OpenAppResponse> OpenApp(AppRequest request, ServerCallContext context)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = request.AppName,
                    Arguments = request.Arguments ?? "",
                    UseShellExecute = true
                };
                var process = Process.Start(startInfo);
                int pid = 0;
                if (process != null)
                {
                    pid = process.Id;
                }
                return Task.FromResult(new OpenAppResponse { Success = true, Message = "App started", ProcessId = pid });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new OpenAppResponse { Success = false, Message = $"Failed to start app: {ex.Message}" });
            }
        }

        public Task<PerformActionResponse> CloseApp(AppRequest request, ServerCallContext context)
        {
            try
            {
                var success = false;
                var exceptions = new List<string>();
                var processes = Process.GetProcessesByName(request.AppName);
                foreach (var p in processes)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit();
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        exceptions.Add(ex.Message);
                    }
                }
                if (!success && processes.Length > 0)
                {
                    return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Failed to close one or more instances. Exceptions: {string.Join(", ", exceptions.ToArray())}" });
                }
                return Task.FromResult(new PerformActionResponse { Success = true, Message = $"All instance of app closed. Instance count: {processes.Length}" });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Failed to close one or more instances. Exception: {ex.Message}" });
            }
        }

        public Task<PerformActionResponse> CloseAppByProcessId(CloseAppByProcessIdRequest request, ServerCallContext context)
        {
            try
            {
                var process = Process.GetProcessById(request.ProcessId);
                process.Kill();
                process.WaitForExit();
                return Task.FromResult(new PerformActionResponse { Success = true, Message = $"Process {request.ProcessId} closed." });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Failed to close process {request.ProcessId}: {ex.Message}" });
            }
        }

    }
}
