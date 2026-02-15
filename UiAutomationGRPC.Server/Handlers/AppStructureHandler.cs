using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Automation;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;
using PropertyCondition = System.Windows.Automation.PropertyCondition;

namespace UiAutomationGRPC.Server.Handlers
{
    /// <summary>
    /// Handles app structure operations (LLM-friendly layer).
    /// </summary>
    public class AppStructureHandler
    {
        private readonly ActionHandler _actionHandler;
        private readonly ILogger<AppStructureHandler> _logger;

        public AppStructureHandler(ILogger<AppStructureHandler> logger, ActionHandler actionHandler = null)
        {
            _logger = logger;
            _actionHandler = actionHandler ?? new ActionHandler();
        }

        public Task<AppStructureResponse> GetAppStructure(AppStructureRequest request, ServerCallContext context)
        {
            try
            {
                Process[] processes = null;

                if (request.UseProcessId && request.ProcessId > 0)
                {
                    var p = Process.GetProcessById(request.ProcessId);
                    if (p != null) processes = new[] { p };
                }
                else if (!string.IsNullOrEmpty(request.AppName))
                {
                    // Strip .exe if present
                    string appName = ElementCache.StripExeExtension(request.AppName);

                    processes = Process.GetProcessesByName(appName);
                    
                    if ((processes == null || processes.Length == 0) && !string.IsNullOrEmpty(request.Arguments))
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = request.AppName,
                            Arguments = request.Arguments,
                            UseShellExecute = true
                        };
                        try 
                        {
                            var p = Process.Start(startInfo);
                            Thread.Sleep(2000); 
                            p.Refresh();
                            processes = new[] { p };
                            
                            if (processes.Length > 0 && !processes[0].HasExited)
                            {
                                var refreshed = Process.GetProcessesByName(appName);
                                if (refreshed.Length > 0) processes = refreshed;
                            }
                        }
                        catch (Exception ex) 
                        { 
                            _logger.LogWarning(ex, "Failed to start application '{AppName}'", request.AppName); 
                        }
                    }
                }

                AutomationElement rootMapElement = null;

                if (processes != null && processes.Length > 0)
                {
                    foreach (var p in processes)
                    {
                        try 
                        {
                            p.Refresh();
                            if (p.HasExited) continue;

                            // Strategy 1: MainWindowHandle
                            if (p.MainWindowHandle != IntPtr.Zero)
                            {
                                try 
                                {
                                    var candidate = AutomationElement.FromHandle(p.MainWindowHandle);
                                    if (candidate != null) 
                                    {
                                        if (!IsUwpSpacer(candidate))
                                        {
                                            rootMapElement = candidate;
                                            break;
                                        }
                                    }
                                }
                                catch (Exception ex) 
                                { 
                                    _logger.LogDebug(ex, "Failed to get element from MainWindowHandle for process {ProcessId}", p.Id); 
                                }
                            }

                            // Strategy 2: Search by PID
                            if (rootMapElement == null)
                            {
                                var condition = new PropertyCondition(AutomationElement.ProcessIdProperty, p.Id);
                                try 
                                {
                                    var candidate = AutomationElement.RootElement.FindFirst(System.Windows.Automation.TreeScope.Children, condition);
                                    if (candidate != null)
                                    {
                                        rootMapElement = candidate;
                                        break;
                                    }
                                }
                                catch (Exception ex) 
                                { 
                                    _logger.LogDebug(ex, "Failed to find element by ProcessId {ProcessId}", p.Id); 
                                }
                            }
                        }
                        catch (Exception ex) 
                        { 
                            _logger.LogDebug(ex, "Error processing process {ProcessId}", p.Id); 
                        }
                    }
                }

                // Strategy 3: Fallback to Window Name search
                if (rootMapElement == null)
                {
                    string nameToSearch = !string.IsNullOrEmpty(request.AppName)
                        ? ElementCache.StripExeExtension(request.AppName)
                        : request.AppName;

                    try 
                    {
                        var nameCondition = new PropertyCondition(AutomationElement.NameProperty, nameToSearch);
                        var candidate = AutomationElement.RootElement.FindFirst(System.Windows.Automation.TreeScope.Children, nameCondition);
                        if (candidate != null)
                        {
                            rootMapElement = candidate;
                        }
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogDebug(ex, "Failed to find window by name '{AppName}'", request.AppName); 
                    }
                }
                
                if (rootMapElement == null)
                    return Task.FromResult(new AppStructureResponse { Success = false, Message = "Main window element not found." });
                
                // Flush stale cache for this process before rebuilding fresh
                try { ElementCache.ClearByProcess(rootMapElement.Current.ProcessId); }
                catch (System.Windows.Automation.ElementNotAvailableException) { }

                var rootNode = BuildAppNode(rootMapElement);
                var json = JsonConvert.SerializeObject(rootNode, Formatting.Indented);

                return Task.FromResult(new AppStructureResponse { Success = true, JsonStructure = json, Message = "Structure retrieved." });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new AppStructureResponse { Success = false, Message = $"Error: {ex.Message}" });
            }
        }

        public async Task<AppStructureResponse> PerformActionWithStructure(PerformActionRequest request, ServerCallContext context)
        {
            var actionResult = await _actionHandler.PerformAction(request, context);
            if (!actionResult.Success)
            {
                return new AppStructureResponse { Success = false, Message = actionResult.Message };
            }

            if (ElementCache.TryGetLive(request.RuntimeId, out var element))
            {
                var window = ScreenshotHandler.GetTopLevelWindow(element);
                if (window != null)
                {
                    await Task.Delay(200);

                    // Flush stale cache for this process before rebuilding fresh
                    try { ElementCache.ClearByProcess(window.Current.ProcessId); }
                    catch (System.Windows.Automation.ElementNotAvailableException) { }

                    var rootNode = BuildAppNode(window);
                    var json = JsonConvert.SerializeObject(rootNode, Formatting.Indented);
                    return new AppStructureResponse { Success = true, JsonStructure = json, Message = "Action performed and structure updated." };
                }
            }
            
            return new AppStructureResponse { Success = true, Message = "Action performed but could not rebuild structure (root not found)." };
        }

        public static AppNode BuildAppNode(AutomationElement element)
        {
            try
            {
                string runtimeId = ElementCache.CacheElement(element);

                var node = new AppNode
                {
                    UniqId = runtimeId,
                    UiAutomationId = element.Current.AutomationId,
                    Name = element.Current.Name,
                    ControlType = element.Current.ControlType.ProgrammaticName,
                    IsClickable = (bool)element.GetCurrentPropertyValue(AutomationElement.IsInvokePatternAvailableProperty) || (bool)element.GetCurrentPropertyValue(AutomationElement.IsTogglePatternAvailableProperty),
                    IsVisible = !element.Current.IsOffscreen
                };

                try 
                {
                    var rect = element.Current.BoundingRectangle;
                    node.BoundingRectangle = $"{rect.Left},{rect.Top},{rect.Width},{rect.Height}";
                } 
                catch 
                { 
                    // BoundingRectangle can fail for offscreen elements - this is expected
                }

                // Use TreeWalker for more reliable child traversal
                foreach (var child in ElementHandler.GetChildElements(element))
                {
                    var childNode = BuildAppNode(child);
                    if (childNode != null)
                        node.Children.Add(childNode);
                }

                return node;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[AppStructureHandler] BuildAppNode failed for element: {ex.Message}");
                return null;
            }
        }

        private static bool IsUwpSpacer(AutomationElement element)
        {
            try 
            {
                return !HasChildren(element);
            } 
            catch (Exception ex)
            { 
                System.Diagnostics.Trace.WriteLine($"[AppStructureHandler] IsUwpSpacer check failed (assuming spacer): {ex.Message}");
                return true; 
            }
        }

        private static bool HasChildren(AutomationElement element)
        {
            var walker = TreeWalker.ControlViewWalker;
            return walker.GetFirstChild(element) != null;
        }
    }
}
