using Grpc.Core;
using FlaUI.Core.AutomationElements;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Handlers
{
    /// <summary>
    /// Handles element finding and property operations.
    /// </summary>
    public class ElementHandler
    {
        private readonly InteractionAccessGuard? _guard;

        public ElementHandler(InteractionAccessGuard? guard = null)
        {
            _guard = guard;
        }

        public ElementResponse FindElement(FindElementRequest request, ServerCallContext context)
        {
            // Business outcomes (not found / blocked / failed) are returned as
            // { Success = false, Message } with an empty RuntimeId — not thrown as RpcException.
            // RpcException is reserved for transport / auth / cancellation.
            try
            {
                // 1. Resolve Start Element
                AutomationElement startElement = UiaRuntime.Desktop;
                if (!string.IsNullOrEmpty(request.StartRuntimeId))
                {
                    if (!ElementCache.TryGetLive(request.StartRuntimeId, out startElement))
                    {
                        return new ElementResponse { Success = false, Message = "Start element not found in cache." };
                    }
                }

                // 2. Build Condition
                var condition = AutomationMapper.MapCondition(request.Condition);

                // 3. Determine Scope
                var scope = AutomationMapper.MapScope(request.Scope);

                // 4. Find
                AutomationElement foundElement = startElement.FindFirst(scope, condition);

                if (foundElement == null)
                {
                    return new ElementResponse { Success = false, Message = "Element not found matching condition." };
                }

                // 5. Validate interaction access against the owning process
                var blocked = InteractionAccessGuard.CheckAccess(_guard, foundElement.Properties.ProcessId.ValueOrDefault);
                if (blocked != null)
                    return new ElementResponse { Success = false, Message = blocked };

                return AutomationMapper.MapToResponse(foundElement);
            }
            catch (Exception ex)
            {
                return new ElementResponse { Success = false, Message = $"Error finding element: {ex.Message}" };
            }
        }

        public ElementListResponse GetChildren(GetChildrenRequest request, ServerCallContext context)
        {
            try
            {
                // 1. Resolve Root Element
                AutomationElement root = UiaRuntime.Desktop;
                if (!string.IsNullOrEmpty(request.RuntimeId))
                {
                    if (!ElementCache.TryGetLive(request.RuntimeId, out root))
                    {
                        return new ElementListResponse { Success = false, Message = "Root element not found in cache." };
                    }
                }

                // 2. Validate interaction access
                if (!string.IsNullOrEmpty(request.RuntimeId))
                {
                    var blocked = InteractionAccessGuard.CheckAccess(_guard, root.Properties.ProcessId.ValueOrDefault);
                    if (blocked != null)
                        return new ElementListResponse { Success = false, Message = blocked };
                }

                // 3. Find All Children using TreeWalker for more reliable traversal
                var children = GetChildElements(root);

                var response = new ElementListResponse
                {
                    Success = true,
                    Message = $"Found {children.Count} children."
                };

                foreach (var element in children)
                {
                    response.Elements.Add(AutomationMapper.MapToResponse(element));
                }

                return response;
            }
            catch (Exception ex)
            {
                return new ElementListResponse { Success = false, Message = $"Error getting children: {ex.Message}" };
            }
        }

        public GetPropertyResponse GetProperty(GetPropertyRequest request, ServerCallContext context)
        {
            if (!ElementCache.TryGetLive(request.RuntimeId, out var element))
            {
                return new GetPropertyResponse { Success = false, Message = "Element not found." };
            }

            // Validate interaction access
            var blocked = InteractionAccessGuard.CheckAccess(_guard, element.Properties.ProcessId.ValueOrDefault);
            if (blocked != null)
                return new GetPropertyResponse { Success = false, Message = blocked };

            try
            {
                // "Value" / "Text" mean "give me the element's text content". Different control
                // families expose it through different patterns: ValuePattern (WinForms textboxes,
                // most edits) or TextPattern (Win32 Edit in Notepad, WPF/UWP documents, rich text).
                // Try both before falling back to a raw property read.
                if (request.PropertyName.Equals("Value", StringComparison.OrdinalIgnoreCase) ||
                    request.PropertyName.Equals("Text", StringComparison.OrdinalIgnoreCase))
                {
                    var valuePattern = element.Patterns.Value.PatternOrDefault;
                    if (valuePattern != null)
                    {
                        return new GetPropertyResponse
                        {
                            Success = true,
                            Value = valuePattern.Value.ValueOrDefault ?? "",
                            Message = "Retrieved (ValuePattern)"
                        };
                    }
                    var textPattern = element.Patterns.Text.PatternOrDefault;
                    if (textPattern != null)
                    {
                        return new GetPropertyResponse
                        {
                            Success = true,
                            Value = textPattern.DocumentRange.GetText(-1) ?? "",
                            Message = "Retrieved (TextPattern)"
                        };
                    }
                }

                var property = AutomationMapper.LookupProperty(request.PropertyName);
                object val = element.FrameworkAutomationElement.GetPropertyValue(property);

                return new GetPropertyResponse
                {
                    Success = true,
                    Value = val?.ToString() ?? "",
                    Message = "Retrieved"
                };
            }
            catch (Exception ex)
            {
                return new GetPropertyResponse { Success = false, Message = $"Error getting property: {ex.Message}" };
            }
        }

        /// <summary>
        /// Gets child elements using TreeWalker for more reliable traversal than FindAll.
        /// </summary>
        public static List<AutomationElement> GetChildElements(AutomationElement parent)
        {
            var children = new List<AutomationElement>();
            var walker = UiaRuntime.Automation.TreeWalkerFactory.GetControlViewWalker();
            var child = walker.GetFirstChild(parent);
            while (child != null)
            {
                children.Add(child);
                child = walker.GetNextSibling(child);
            }
            return children;
        }
    }
}
