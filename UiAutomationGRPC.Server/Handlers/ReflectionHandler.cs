using System.Reflection;
using FlaUI.Core.Definitions;
using FlaUI.Core.Identifiers;
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
        private readonly InteractionAccessGuard? _guard;

        public ReflectionHandler(InteractionAccessGuard? guard = null)
        {
            _guard = guard;
        }
        public ReflectionResponse Reflect(ReflectionRequest request, ServerCallContext context)
        {
            var response = new ReflectionResponse();
            try
            {
                switch (request.Target)
                {
                    case ReflectionTarget.AutomationProperties:
                        // Enumerate every element-level PropertyId the UIA3 property library exposes.
                        AddIdentifiers(UiaRuntime.Properties.Element, response);
                        break;

                    case ReflectionTarget.ControlTypes:
                        foreach (var name in Enum.GetNames<ControlType>())
                        {
                            response.Entries.Add(new ReflectionEntry { Name = name, Value = "ControlType." + name });
                        }
                        break;

                    case ReflectionTarget.Patterns:
                        AddIdentifiers(UiaRuntime.Automation.PatternLibrary, response);
                        break;

                    case ReflectionTarget.ElementSupportedPatterns:
                        if (string.IsNullOrEmpty(request.RuntimeId) || !ElementCache.TryGetLive(request.RuntimeId, out var elementPatterns))
                        {
                            response.Success = false;
                            response.Message = "Element not found in cache (provide runtime_id).";
                            return response;
                        }
                        // Validate interaction access for element-specific reflection
                        var blockedP = InteractionAccessGuard.CheckAccess(_guard, elementPatterns.Properties.ProcessId.ValueOrDefault);
                        if (blockedP != null)
                        {
                            response.Success = false;
                            response.Message = blockedP;
                            return response;
                        }
                        foreach (var p in elementPatterns.GetSupportedPatterns())
                        {
                            response.Entries.Add(new ReflectionEntry { Name = p.Name ?? p.Id.ToString(), Value = p.Id.ToString() });
                        }
                        break;

                    case ReflectionTarget.ElementSupportedProperties:
                        if (string.IsNullOrEmpty(request.RuntimeId) || !ElementCache.TryGetLive(request.RuntimeId, out var elementProps))
                        {
                            response.Success = false;
                            response.Message = "Element not found in cache (provide runtime_id).";
                            return response;
                        }
                        // Validate interaction access for element-specific reflection
                        var blockedPr = InteractionAccessGuard.CheckAccess(_guard, elementProps.Properties.ProcessId.ValueOrDefault);
                        if (blockedPr != null)
                        {
                            response.Success = false;
                            response.Message = blockedPr;
                            return response;
                        }
                        foreach (var ap in elementProps.GetSupportedPropertiesDirect())
                        {
                            response.Entries.Add(new ReflectionEntry { Name = ap.Name ?? ap.Id.ToString(), Value = ap.Id.ToString() });
                        }
                        break;

                    default:
                        response.Success = false;
                        response.Message = "Unknown ReflectionTarget.";
                        return response;
                }

                response.Success = true;
                response.Message = "OK";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Reflection error: {ex.Message}";
                return response;
            }
        }

        /// <summary>
        /// Adds every <see cref="PropertyId"/>/<see cref="PatternId"/> exposed as a property of the
        /// given library object (FlaUI's libraries are plain interfaces with one property per id).
        /// </summary>
        private static void AddIdentifiers(object library, ReflectionResponse response)
        {
            foreach (var prop in library.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!typeof(IdentifierBase).IsAssignableFrom(prop.PropertyType)) continue;
                if (prop.GetValue(library) is IdentifierBase id)
                {
                    response.Entries.Add(new ReflectionEntry { Name = prop.Name, Value = id.Id.ToString() });
                }
            }
        }
    }
}
