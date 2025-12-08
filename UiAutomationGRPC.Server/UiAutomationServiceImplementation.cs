using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using Grpc.Core;
using UiAutomation;
using PropertyCondition = System.Windows.Automation.PropertyCondition;
using System.Reflection;

namespace UiAutomationGRPC.Server
{
    public class UiAutomationServiceImplementation : UiAutomationService.UiAutomationServiceBase
    {
        // Cache to store generic AutomationElements to retrieve them by ID for subsequent calls
        private static readonly ConcurrentDictionary<string, AutomationElement> _elementCache = new ConcurrentDictionary<string, AutomationElement>();

        public override Task<ElementResponse> FindElement(FindElementRequest request, ServerCallContext context)
        {
            try
            {
                // 1. Resolve Start Element
                AutomationElement startElement = AutomationElement.RootElement;
                if (!string.IsNullOrEmpty(request.StartRuntimeId))
                {
                    if (!_elementCache.TryGetValue(request.StartRuntimeId, out startElement))
                    {
                         throw new RpcException(new Status(StatusCode.NotFound, "Start element not found in cache."));
                    }
                }

                // 2. Build Condition
                System.Windows.Automation.Condition condition = MapCondition(request.Condition);

                // 3. Determine Scope
                System.Windows.Automation.TreeScope scope = MapScope(request.Scope);

                // 4. Find
                AutomationElement foundElement = startElement.FindFirst(scope, condition);

                if (foundElement == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "Element not found matching condition."));
                }

                return Task.FromResult(MapToResponse(foundElement));
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Error finding element: {ex.Message}"));
            }
        }

        public override Task<PerformActionResponse> PerformAction(PerformActionRequest request, ServerCallContext context)
        {
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
                    case ActionType.ExpandCollapse:
                        var ecPattern = GetPattern<ExpandCollapsePattern>(element, ExpandCollapsePattern.Pattern);
                        if (request.Arguments.Count > 0 && request.Arguments[0].ToLower() == "expand")
                             ecPattern.Expand();
                        else if (request.Arguments.Count > 0 && request.Arguments[0].ToLower() == "collapse")
                             ecPattern.Collapse();
                        else // Default toggle behavior if no arg? or just generic Expand
                             ecPattern.Expand(); 
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
                             // Fallback to simpler click logic if needed, or throw
                             throw new NotSupportedException("Click not fully implemented without P/Invoke. Using InvokePattern is recommended.");
                         }
                         break;
                    default:
                        throw new NotSupportedException($"Action {request.Action} is not supported.");
                }

                return Task.FromResult(new PerformActionResponse { Success = true, Message = "Action performed successfully." });
            }
            catch (Exception ex)
            {
                 return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Error performing action: {ex.Message}" });
            }
        }

        public override Task<GetPropertyResponse> GetProperty(GetPropertyRequest request, ServerCallContext context)
        {
            if (!_elementCache.TryGetValue(request.RuntimeId, out var element))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Element not found."));
            }

            try
            {
                // Reflection or Map lookup for property
                AutomationProperty property = LookupProperty(request.PropertyName);
                object val = element.GetCurrentPropertyValue(property);
                
                return Task.FromResult(new GetPropertyResponse 
                { 
                    Success = true, 
                    Value = val?.ToString() ?? "", 
                    Message = "Retrieved" 
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new GetPropertyResponse { Success = false, Message = $"Error getting property: {ex.Message}" });
            }
        }

        public override Task<OpenAppResponse> OpenApp(AppRequest request, ServerCallContext context)
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
                    // Allow some time for UI to appear? 
                }
                return Task.FromResult(new OpenAppResponse { Success = true, Message = "App started", ProcessId = pid });
            }
            catch (Exception ex)
            {
                  return Task.FromResult(new OpenAppResponse { Success = false, Message = $"Failed to start app: {ex.Message}" });
            }
        }

        // Reflection API implementation
        public override Task<ReflectionResponse> Reflect(ReflectionRequest request, ServerCallContext context)
        {
            var response = new ReflectionResponse();
            try
            {
                switch (request.Target)
                {
                    case ReflectionTarget.AutomationProperties:
                        // Enumerate static AutomationProperty fields on AutomationElement (and other related types)
                        AddStaticAutomationProperties(typeof(AutomationElement), response);
                        // include common properties from other types if present (ControlType, etc. are separate)
                        break;

                    case ReflectionTarget.ControlTypes:
                        foreach (var f in typeof(ControlType).GetFields(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (f.FieldType == typeof(ControlType))
                            {
                                var ct = (ControlType)f.GetValue(null);
                                response.Entries.Add(new ReflectionEntry { Name = f.Name, Value = ct.ProgrammaticName ?? ct.ToString() });
                            }
                        }
                        break;

                    case ReflectionTarget.Patterns:
                        // Scan the assembly for public static fields named "Pattern" of type AutomationPattern
                        foreach (var t in typeof(AutomationElement).Assembly.GetTypes())
                        {
                            var field = t.GetField("Pattern", BindingFlags.Public | BindingFlags.Static);
                            if (field != null && field.FieldType == typeof(AutomationPattern))
                            {
                                var ap = (AutomationPattern)field.GetValue(null);
                                // Use the type name (e.g., InvokePattern) and programmatic name / id
                                response.Entries.Add(new ReflectionEntry { Name = t.Name, Value = ap.ProgrammaticName ?? ap.Id.ToString() });
                            }
                        }
                        break;

                    case ReflectionTarget.ElementSupportedPatterns:
                        if (string.IsNullOrEmpty(request.RuntimeId) || !_elementCache.TryGetValue(request.RuntimeId, out var elementPatterns))
                        {
                            response.Success = false;
                            response.Message = "Element not found in cache (provide runtime_id).";
                            return Task.FromResult(response);
                        }
                        var supportedPatterns = elementPatterns.GetSupportedPatterns();
                        foreach (var p in supportedPatterns)
                        {
                            response.Entries.Add(new ReflectionEntry { Name = p.ProgrammaticName ?? p.Id.ToString(), Value = p.Id.ToString() });
                        }
                        break;

                    case ReflectionTarget.ElementSupportedProperties:
                        if (string.IsNullOrEmpty(request.RuntimeId) || !_elementCache.TryGetValue(request.RuntimeId, out var elementProps))
                        {
                            response.Success = false;
                            response.Message = "Element not found in cache (provide runtime_id).";
                            return Task.FromResult(response);
                        }
                        var supportedProps = elementProps.GetSupportedProperties();
                        foreach (var ap in supportedProps)
                        {
                            response.Entries.Add(new ReflectionEntry { Name = ap.ProgrammaticName ?? ap.Id.ToString(), Value = ap.Id.ToString() });
                        }
                        break;

                    default:
                        response.Success = false;
                        response.Message = "Unknown ReflectionTarget.";
                        return Task.FromResult(response);
                }

                response.Success = true;
                response.Message = "OK";
                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Reflection error: {ex.Message}";
                return Task.FromResult(response);
            }
        }

        // Helpers for Reflection
        private void AddStaticAutomationProperties(Type t, ReflectionResponse response)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType == typeof(AutomationProperty))
                {
                    var ap = (AutomationProperty)f.GetValue(null);
                    response.Entries.Add(new ReflectionEntry { Name = f.Name, Value = ap.ProgrammaticName ?? ap.Id.ToString() });
                }
            }
        }

        // Helpers

        private System.Windows.Automation.Condition MapCondition(UiAutomation.Condition protoCondition)
        {
            if (protoCondition == null) return System.Windows.Automation.Condition.TrueCondition;

            switch (protoCondition.ConditionTypeCase)
            {
                case UiAutomation.Condition.ConditionTypeOneofCase.TrueCondition:
                    return System.Windows.Automation.Condition.TrueCondition;
                
                case UiAutomation.Condition.ConditionTypeOneofCase.PropertyCondition:
                    var pc = protoCondition.PropertyCondition;
                    AutomationProperty prop = LookupProperty(pc.PropertyName);
                    object val = ParseValue(pc.PropertyValue, pc.PropertyType);
                    return new PropertyCondition(prop, val);

                case UiAutomation.Condition.ConditionTypeOneofCase.AndCondition:
                    var subsAnd = pc_List(protoCondition.AndCondition.Conditions);
                    return new AndCondition(subsAnd.ToArray());

                case UiAutomation.Condition.ConditionTypeOneofCase.OrCondition:
                     var subsOr = pc_List(protoCondition.OrCondition.Conditions);
                    return new OrCondition(subsOr.ToArray());

                case UiAutomation.Condition.ConditionTypeOneofCase.NotCondition:
                    return new NotCondition(MapCondition(protoCondition.NotCondition));

                default:
                    return System.Windows.Automation.Condition.TrueCondition;
            }
        }

        private List<System.Windows.Automation.Condition> pc_List(Google.Protobuf.Collections.RepeatedField<UiAutomation.Condition> conditions)
        {
            var list = new List<System.Windows.Automation.Condition>();
            foreach(var c in conditions) list.Add(MapCondition(c));
            return list;
        }

        private object ParseValue(string value, PropertyType type)
        {
            switch(type)
            {
                case PropertyType.Int: return int.Parse(value);
                case PropertyType.Bool: return bool.Parse(value);
                default: return value;
            }
        }

        private AutomationProperty LookupProperty(string name)
        {
            switch (name.ToLower())
            {
                case "name": return AutomationElement.NameProperty;
                case "automationid": return AutomationElement.AutomationIdProperty;
                case "classname": return AutomationElement.ClassNameProperty;
                case "controltype": return AutomationElement.ControlTypeProperty;
                case "isenabled": return AutomationElement.IsEnabledProperty;
                // Add more as needed
                default: throw new ArgumentException($"Unknown property: {name}");
            }
        }

        private System.Windows.Automation.TreeScope MapScope(UiAutomation.TreeScope scope)
        {
            switch(scope)
            {
                case UiAutomation.TreeScope.Children: return System.Windows.Automation.TreeScope.Children;
                case UiAutomation.TreeScope.Descendants: return System.Windows.Automation.TreeScope.Descendants;
                case UiAutomation.TreeScope.Subtree: return System.Windows.Automation.TreeScope.Subtree;
                case UiAutomation.TreeScope.Parent: return System.Windows.Automation.TreeScope.Parent;
                case UiAutomation.TreeScope.Ancestors: return System.Windows.Automation.TreeScope.Ancestors;
                case UiAutomation.TreeScope.Element: return System.Windows.Automation.TreeScope.Element;
                default: return System.Windows.Automation.TreeScope.Children;
            }
        }

        private T GetPattern<T>(AutomationElement element, AutomationPattern pattern) where T : BasePattern
        {
             object pObj;
             if (element.TryGetCurrentPattern(pattern, out pObj))
             {
                 return (T)pObj;
             }
             throw new InvalidOperationException($"Element does not support pattern {pattern.ProgrammaticName}");
        }

        private ElementResponse MapToResponse(AutomationElement element)
        {
            try
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
            catch (ElementNotAvailableException)
            {
                return new ElementResponse { Name = "ElementNotAvailable" };
            }
        }
    }
}
