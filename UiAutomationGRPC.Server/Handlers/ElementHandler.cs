using Grpc.Core;
using System.Windows.Automation;
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
                AutomationElement startElement = AutomationElement.RootElement;
                if (!string.IsNullOrEmpty(request.StartRuntimeId))
                {
                    if (!ElementCache.TryGetLive(request.StartRuntimeId, out startElement))
                    {
                        return new ElementResponse { Success = false, Message = "Start element not found in cache." };
                    }
                }

                // 2. Build Condition
                System.Windows.Automation.Condition condition = AutomationMapper.MapCondition(request.Condition);

                // 3. Determine Scope
                System.Windows.Automation.TreeScope scope = AutomationMapper.MapScope(request.Scope);

                // 4. Find
                AutomationElement foundElement = startElement.FindFirst(scope, condition);

                if (foundElement == null)
                {
                    return new ElementResponse { Success = false, Message = "Element not found matching condition." };
                }

                // 5. Validate interaction access against the owning process
                var blocked = InteractionAccessGuard.CheckAccess(_guard, foundElement.Current.ProcessId);
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
                AutomationElement root = AutomationElement.RootElement;
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
                    var blocked = InteractionAccessGuard.CheckAccess(_guard, root.Current.ProcessId);
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

                foreach (AutomationElement element in children)
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
            var blocked = InteractionAccessGuard.CheckAccess(_guard, element.Current.ProcessId);
            if (blocked != null)
                return new GetPropertyResponse { Success = false, Message = blocked };

            try
            {
                AutomationProperty property = AutomationMapper.LookupProperty(request.PropertyName);
                object val = element.GetCurrentPropertyValue(property);

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
            var walker = TreeWalker.ControlViewWalker;
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
