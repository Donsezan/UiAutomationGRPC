using Grpc.Core;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using UiAutomation;
using UiAutomationGRPC.LayerServer.Models;
using Newtonsoft.Json;
using Automation = System.Windows.Automation;

namespace UiAutomationGRPC.LayerServer.Services
{
    public class LayerService : UiAutomationService.UiAutomationServiceBase
    {
        private static readonly ConcurrentDictionary<string, AutomationElement> _elementCache = new ConcurrentDictionary<string, AutomationElement>();

        // -- Existing Methods Implementation (Ported/Reused) --

        public override Task<ElementResponse> FindElement(FindElementRequest request, ServerCallContext context)
        {
            try
            {
                AutomationElement startElement = AutomationElement.RootElement;
                if (!string.IsNullOrEmpty(request.StartRuntimeId))
                {
                    if (!_elementCache.TryGetValue(request.StartRuntimeId, out startElement))
                    {
                        throw new RpcException(new Status(StatusCode.NotFound, "Start element not found in cache."));
                    }
                }

                Automation.Condition condition = MapCondition(request.Condition);
                Automation.TreeScope scope = MapScope(request.Scope);
                AutomationElement foundElement = startElement.FindFirst(scope, condition);

                if (foundElement == null)
                    throw new RpcException(new Status(StatusCode.NotFound, "Element not found."));

                return Task.FromResult(MapToResponse(foundElement));
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Error: {ex.Message}"));
            }
        }

        public override Task<PerformActionResponse> PerformAction(PerformActionRequest request, ServerCallContext context)
        {
            return PerformActionInternal(request);
        }

        private Task<PerformActionResponse> PerformActionInternal(PerformActionRequest request)
        {
             if (string.IsNullOrEmpty(request.RuntimeId))
            {
                return HandleGlobalAction(request);
            }

            if (!_elementCache.TryGetValue(request.RuntimeId, out var element))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Element not found in cache."));
            }

            try
            {
                switch (request.Action)
                {
                    case ActionType.Invoke:
                        GetPattern<InvokePattern>(element, InvokePattern.Pattern).Invoke();
                        break;
                    case ActionType.Toggle:
                        GetPattern<TogglePattern>(element, TogglePattern.Pattern).Toggle();
                        break;
                    case ActionType.SetValue:
                        if (request.Arguments.Count == 0) throw new ArgumentException("SetValue requires an argument.");
                        GetPattern<ValuePattern>(element, ValuePattern.Pattern).SetValue(request.Arguments[0]);
                        break;
                    case ActionType.Select:
                        GetPattern<SelectionItemPattern>(element, SelectionItemPattern.Pattern).Select();
                        break;
                    case ActionType.SetFocus:
                        element.SetFocus();
                        break;
                    case ActionType.Click:
                         if (element.TryGetCurrentPattern(InvokePattern.Pattern, out object invPat))
                         {
                             ((InvokePattern)invPat).Invoke();
                         }
                         else
                         {
                             var rect = element.Current.BoundingRectangle;
                             if(rect.Width > 0) {
                                 int x = (int)(rect.X + rect.Width / 2);
                                 int y = (int)(rect.Y + rect.Height / 2);
                                 NativeMethods.SetCursorPos(x, y);
                                 NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                                 NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                             }
                         }
                         break;
                     default:
                         break;
                }
                return Task.FromResult(new PerformActionResponse { Success = true, Message = "Action performed." });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Error: {ex.Message}" });
            }
        }

        // -- New Methods for LayerServer --

        public override Task<AppStructureResponse> GetAppStructure(AppStructureRequest request, ServerCallContext context)
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
                    string appName = request.AppName;
                    if (appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        appName = Path.GetFileNameWithoutExtension(appName);

                    processes = Process.GetProcessesByName(appName);
                    
                    if ((processes == null || processes.Length == 0) && !string.IsNullOrEmpty(request.Arguments))
                    {
                         var startInfo = new ProcessStartInfo
                         {
                             FileName = request.AppName, // Here use full name if needed
                             Arguments = request.Arguments,
                             UseShellExecute = true
                         };
                         try 
                         {
                             var p = Process.Start(startInfo);
                             Thread.Sleep(2000); 
                             p.Refresh();
                             processes = new[] { p };
                             
                             // Re-fetch by name to get correct UWP groups if needed
                             if (processes.Length > 0 && !processes[0].HasExited)
                             {
                                 var refreshed = Process.GetProcessesByName(appName);
                                 if (refreshed.Length > 0) processes = refreshed;
                             }
                         }
                         catch { /* Start failed */ }
                    }
                }

                AutomationElement rootMapElement = null;
                Process activeProcess = null;

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
                                            activeProcess = p;
                                            break;
                                         }
                                    }
                                }
                                catch {}
                            }

                            // Strategy 2: Search by PID
                            if (rootMapElement == null)
                            {
                                var condition = new Automation.PropertyCondition(AutomationElement.ProcessIdProperty, p.Id);
                                try 
                                {
                                    var candidate = AutomationElement.RootElement.FindFirst(Automation.TreeScope.Children, condition);
                                    if (candidate != null)
                                    {
                                        rootMapElement = candidate;
                                        activeProcess = p;
                                        break;
                                    }
                                }
                                catch {}
                            }
                        }
                        catch { }
                    }
                }

                // Strategy 3: Fallback to Window Name search
                if (rootMapElement == null)
                {
                     string nameToSearch = request.AppName;
                     if (nameToSearch.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                         nameToSearch = Path.GetFileNameWithoutExtension(nameToSearch);

                     try 
                     {
                         var nameCondition = new Automation.PropertyCondition(AutomationElement.NameProperty, nameToSearch);
                         var candidate = AutomationElement.RootElement.FindFirst(Automation.TreeScope.Children, nameCondition);
                         if (candidate != null)
                         {
                             rootMapElement = candidate;
                         }
                     }
                     catch { }
                }
                
                // Final check: valid root element
                if (rootMapElement == null)
                    return Task.FromResult(new AppStructureResponse { Success = false, Message = "Main window element not found." });
                
                var rootNode = BuildAppNode(rootMapElement);
                
                var json = JsonConvert.SerializeObject(rootNode, Formatting.Indented);

                return Task.FromResult(new AppStructureResponse { Success = true, JsonStructure = json, Message = "Structure retrieved." });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new AppStructureResponse { Success = false, Message = $"Error: {ex.Message}" });
            }
        }

        public override async Task<AppStructureResponse> PerformActionWithStructure(PerformActionRequest request, ServerCallContext context)
        {
            var actionResult = await PerformActionInternal(request);
            if (!actionResult.Success)
            {
                return new AppStructureResponse { Success = false, Message = actionResult.Message };
            }

            if (_elementCache.TryGetValue(request.RuntimeId, out var element))
            {
                var window = GetTopLevelWindow(element);
                if (window != null)
                {
                    await Task.Delay(200);
                    var rootNode = BuildAppNode(window);
                    var json = JsonConvert.SerializeObject(rootNode, Formatting.Indented);
                    return new AppStructureResponse { Success = true, JsonStructure = json, Message = "Action performed and structure updated." };
                }
            }
            
            return new AppStructureResponse { Success = true, Message = "Action performed but could not rebuild structure (root not found)." };
        }

        // -- Helpers --

        private AppNode BuildAppNode(AutomationElement element)
        {
            try
            {
                string runtimeId = string.Join(",", element.GetRuntimeId());
                _elementCache.TryAdd(runtimeId, element);

                var node = new AppNode
                {
                    UniqId = runtimeId,
                    UiAutomationId = element.Current.AutomationId,
                    Name = element.Current.Name,
                    ControlType = element.Current.ControlType.ProgrammaticName,
                    IsClickable = (bool)element.GetCurrentPropertyValue(AutomationElement.IsInvokePatternAvailableProperty) || (bool)element.GetCurrentPropertyValue(AutomationElement.IsTogglePatternAvailableProperty),
                    IsVisible = !element.Current.IsOffscreen
                };

                try {
                    var rect = element.Current.BoundingRectangle;
                    node.BoundingRectangle = $"{rect.Left},{rect.Top},{rect.Width},{rect.Height}";
                } catch {}

                var children = element.FindAll(Automation.TreeScope.Children, Automation.Condition.TrueCondition);
                foreach (AutomationElement child in children)
                {
                     var childNode = BuildAppNode(child);
                     if (childNode != null)
                        node.Children.Add(childNode);
                }

                return node;
            }
            catch
            {
                return null;
            }
        }
        
        private AutomationElement GetTopLevelWindow(AutomationElement element)
        {
            var walker = TreeWalker.ControlViewWalker;
            var current = element;
            while (current != null)
            {
                if (current == AutomationElement.RootElement) return element;
                var parent = walker.GetParent(current);
                if (parent == AutomationElement.RootElement) return current;
                current = parent;
            }
            return element;
        }

        private bool IsUwpSpacer(AutomationElement element)
        {
            try {
                // Heuristic: If it has no children in Control View, it might be a wrapper.
                // But simplified: Just return false and rely on PID search if FromHandle was valid but empty?
                // Actually, let's just rely on the fallback if FromHandle worked.
                // For now, return false to trust valid handle, unless user reports otherwise. 
                // But in this case, we had a valid handle but empty children.
                // Let's check for children count?
                var children = element.FindAll(Automation.TreeScope.Children, Automation.Condition.TrueCondition);
                return children.Count == 0;
            } catch { return true; }
        }

        private Automation.Condition MapCondition(UiAutomation.Condition protoCondition)
        {
            if (protoCondition == null) return Automation.Condition.TrueCondition;
            switch (protoCondition.ConditionTypeCase)
            {
                 // Simplification: only properties for now
                 case UiAutomation.Condition.ConditionTypeOneofCase.PropertyCondition:
                    var pc = protoCondition.PropertyCondition;
                    return new Automation.PropertyCondition(LookupProperty(pc.PropertyName), ParseValue(pc.PropertyValue, pc.PropertyType));
                 default:
                    return Automation.Condition.TrueCondition;
            }
        }
        
        private AutomationProperty LookupProperty(string name)
        {
             switch (name.ToLower())
            {
                case "name": return AutomationElement.NameProperty;
                case "automationid": return AutomationElement.AutomationIdProperty;
                case "classname": return AutomationElement.ClassNameProperty;
                default: return AutomationElement.NameProperty;
            }
        }

        private object ParseValue(string value, PropertyType type)
        {
            return value;
        }

        private Automation.TreeScope MapScope(UiAutomation.TreeScope scope)
        {
            return Automation.TreeScope.Children; 
        }
        
        private ElementResponse MapToResponse(AutomationElement element)
        {
             int[] runtimeId = element.GetRuntimeId();
             string runtimeIdStr = string.Join(",", runtimeId);
             _elementCache.TryAdd(runtimeIdStr, element);
             return new ElementResponse
             {
                 Name = element.Current.Name ?? "",
                 AutomationId = element.Current.AutomationId ?? "",
                 ClassName = element.Current.ClassName ?? "",
                 ControlType = element.Current.ControlType.ProgrammaticName,
                 RuntimeId = runtimeIdStr
             };
        }

        private T GetPattern<T>(AutomationElement element, AutomationPattern pattern)
        {
             return (T)element.GetCurrentPattern(pattern);
        }
        
        public override Task<GetPropertyResponse> GetProperty(GetPropertyRequest request, ServerCallContext context) => Task.FromResult(new GetPropertyResponse());
        public override Task<OpenAppResponse> OpenApp(AppRequest request, ServerCallContext context) 
        {
            var p = Process.Start(request.AppName, request.Arguments);
            return Task.FromResult(new OpenAppResponse { Success = true, ProcessId = p.Id });
        }
        public override Task<PerformActionResponse> CloseApp(AppRequest request, ServerCallContext context)
        {
             // Strategy 1: Kill by Process Name
             var processes = Process.GetProcessesByName(request.AppName);
             if (processes.Length > 0)
             {
                 foreach(var p in processes) 
                 {
                     try { p.Kill(); } catch {}
                 }
                 return Task.FromResult(new PerformActionResponse { Success = true, Message = "Processes killed." });
             }

             // Strategy 2: Close by Window Name (Fallback for UWP)
             string nameToSearch = request.AppName;
             if (nameToSearch.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                 nameToSearch = Path.GetFileNameWithoutExtension(nameToSearch);

             try 
             {
                 var nameCondition = new Automation.PropertyCondition(AutomationElement.NameProperty, nameToSearch);
                 var candidate = AutomationElement.RootElement.FindFirst(Automation.TreeScope.Children, nameCondition);
                 if (candidate != null)
                 {
                     if (candidate.TryGetCurrentPattern(WindowPattern.Pattern, out object winPat))
                     {
                         ((WindowPattern)winPat).Close();
                         return Task.FromResult(new PerformActionResponse { Success = true, Message = "Window closed via WindowPattern." });
                     }
                 }
             }
             catch (Exception ex) 
             {
                 return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Error closing window: {ex.Message}" });
             }
             
             return Task.FromResult(new PerformActionResponse { Success = false, Message = "App not found to close." });
        }
        public override Task<PerformActionResponse> CloseAppByProcessId(CloseAppByProcessIdRequest request, ServerCallContext context)
        {
             Process.GetProcessById(request.ProcessId).Kill();
             return Task.FromResult(new PerformActionResponse { Success = true });
        }
        public override Task<ScreenshotResponse> TakeScreenshot(ScreenshotRequest request, ServerCallContext context)
        {
             return Task.FromResult(new ScreenshotResponse { Success = false, Message = "Not fully implemented in LayerServer yet" });
        }
        public override Task<ReflectionResponse> Reflect(ReflectionRequest request, ServerCallContext context) => Task.FromResult(new ReflectionResponse());
        public override Task<ElementListResponse> GetChildren(GetChildrenRequest request, ServerCallContext context) => Task.FromResult(new ElementListResponse());
        public override Task<PerformActionResponse> SendKeys(SendKeysRequest request, ServerCallContext context) => Task.FromResult(new PerformActionResponse());
        
        private Task<PerformActionResponse> HandleGlobalAction(PerformActionRequest request)
        {
            return Task.FromResult(new PerformActionResponse());
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool SetCursorPos(int x, int y);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

            public const int MOUSEEVENTF_LEFTDOWN = 0x02;
            public const int MOUSEEVENTF_LEFTUP = 0x04;
        }

    }
}
