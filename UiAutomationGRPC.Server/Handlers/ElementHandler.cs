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
        public Task<ElementResponse> FindElement(FindElementRequest request, ServerCallContext context)
        {
            try
            {
                // 1. Resolve Start Element
                AutomationElement startElement = AutomationElement.RootElement;
                if (!string.IsNullOrEmpty(request.StartRuntimeId))
                {
                    if (!ElementCache.TryGet(request.StartRuntimeId, out startElement))
                    {
                        throw new RpcException(new Status(StatusCode.NotFound, "Start element not found in cache."));
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
                    throw new RpcException(new Status(StatusCode.NotFound, "Element not found matching condition."));
                }

                return Task.FromResult(AutomationMapper.MapToResponse(foundElement));
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Error finding element: {ex.Message}"));
            }
        }

        public Task<ElementListResponse> GetChildren(GetChildrenRequest request, ServerCallContext context)
        {
            try
            {
                // 1. Resolve Root Element
                AutomationElement root = AutomationElement.RootElement;
                if (!string.IsNullOrEmpty(request.RuntimeId))
                {
                    if (!ElementCache.TryGet(request.RuntimeId, out root))
                    {
                        return Task.FromResult(new ElementListResponse { Success = false, Message = "Root element not found in cache." });
                    }
                }

                // 2. Find All Children using TreeWalker for more reliable traversal
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

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ElementListResponse { Success = false, Message = $"Error getting children: {ex.Message}" });
            }
        }

        public Task<GetPropertyResponse> GetProperty(GetPropertyRequest request, ServerCallContext context)
        {
            if (!ElementCache.TryGet(request.RuntimeId, out var element))
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Element not found."));
            }

            try
            {
                AutomationProperty property = AutomationMapper.LookupProperty(request.PropertyName);
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
