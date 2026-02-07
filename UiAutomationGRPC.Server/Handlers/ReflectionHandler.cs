using System.Reflection;
using System.Windows.Automation;
using Grpc.Core;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Handlers
{
    /// <summary>
    /// Handles reflection API for property/pattern discovery.
    /// </summary>
    public class ReflectionHandler
    {
        public Task<ReflectionResponse> Reflect(ReflectionRequest request, ServerCallContext context)
        {
            var response = new ReflectionResponse();
            try
            {
                switch (request.Target)
                {
                    case ReflectionTarget.AutomationProperties:
                        AddStaticAutomationProperties(typeof(AutomationElement), response);
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
                        foreach (var t in typeof(AutomationElement).Assembly.GetTypes())
                        {
                            var field = t.GetField("Pattern", BindingFlags.Public | BindingFlags.Static);
                            if (field != null && field.FieldType == typeof(AutomationPattern))
                            {
                                var ap = (AutomationPattern)field.GetValue(null);
                                response.Entries.Add(new ReflectionEntry { Name = t.Name, Value = ap.ProgrammaticName ?? ap.Id.ToString() });
                            }
                        }
                        break;

                    case ReflectionTarget.ElementSupportedPatterns:
                        if (string.IsNullOrEmpty(request.RuntimeId) || !ElementCache.TryGet(request.RuntimeId, out var elementPatterns))
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
                        if (string.IsNullOrEmpty(request.RuntimeId) || !ElementCache.TryGet(request.RuntimeId, out var elementProps))
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
    }
}
