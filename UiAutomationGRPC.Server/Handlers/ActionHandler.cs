using System.Drawing;
using System.Windows.Automation;
using Grpc.Core;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Handlers
{
    /// <summary>
    /// Handles action execution on elements and global actions.
    /// </summary>
    public class ActionHandler
    {
        public Task<PerformActionResponse> PerformAction(PerformActionRequest request, ServerCallContext context)
        {
            // If RuntimeId is empty, we handle global/mouse actions that don't require an element.
            if (string.IsNullOrEmpty(request.RuntimeId))
            {
                return HandleGlobalAction(request);
            }

            if (!ElementCache.TryGet(request.RuntimeId, out var element))
            {
                throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Element not found in cache."));
            }

            try
            {
                switch (request.Action)
                {
                    case ActionType.Invoke:
                        AutomationMapper.GetPattern<InvokePattern>(element, InvokePattern.Pattern).Invoke();
                        break;
                    case ActionType.Toggle:
                        AutomationMapper.GetPattern<TogglePattern>(element, TogglePattern.Pattern).Toggle();
                        break;
                    case ActionType.ExpandCollapse:
                        var ecPattern = AutomationMapper.GetPattern<ExpandCollapsePattern>(element, ExpandCollapsePattern.Pattern);
                        if (request.Arguments.Count > 0 && request.Arguments[0].ToLower() == "expand")
                            ecPattern.Expand();
                        else if (request.Arguments.Count > 0 && request.Arguments[0].ToLower() == "collapse")
                            ecPattern.Collapse();
                        else 
                            ecPattern.Expand(); 
                        break;
                    case ActionType.SetValue:
                        if (request.Arguments.Count == 0) throw new ArgumentException("SetValue requires an argument.");
                        AutomationMapper.GetPattern<ValuePattern>(element, ValuePattern.Pattern).SetValue(request.Arguments[0]);
                        break;
                    case ActionType.Select:
                        AutomationMapper.GetPattern<SelectionItemPattern>(element, SelectionItemPattern.Pattern).Select();
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
                            throw new NotSupportedException("Click not fully implemented without P/Invoke. Using InvokePattern is recommended.");
                        }
                        break;
                    case ActionType.MoveTo:                        
                        var rect = element.Current.BoundingRectangle;
                        if (rect.Width > 0 && rect.Height > 0)
                        {
                            int x = (int)(rect.X + rect.Width / 2);
                            int y = (int)(rect.Y + rect.Height / 2);
                            VirtualMouse.MoveTo(x, y);
                        }
                        break;
                    case ActionType.LeftClick:
                        ClickElementAtCenter(element);
                        break;
                    case ActionType.RightClick:
                        ClickElementAtCenter(element, rightClick: true);
                        break;
                    case ActionType.DoubleClick:
                        DoubleClickElementAtCenter(element);
                        break;
                    default:
                        throw new NotSupportedException($"Action {request.Action} is not supported on an element.");
                }

                return Task.FromResult(new PerformActionResponse { Success = true, Message = "Action performed successfully." });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Error performing action: {ex.Message}" });
            }
        }

        /// <summary>
        /// Handles global mouse actions that don't require an element context.
        /// All mouse operations are delegated to VirtualMouse helper.
        /// </summary>
        public Task<PerformActionResponse> HandleGlobalAction(PerformActionRequest request)
        {
            try
            {
                switch (request.Action)
                {
                    case ActionType.Move:
                        if (request.Arguments.Count < 2) throw new ArgumentException("Move requires x and y arguments.");
                        int x = int.Parse(request.Arguments[0]);
                        int y = int.Parse(request.Arguments[1]);
                        VirtualMouse.MoveTo(x, y);
                        break;
                    case ActionType.LeftClick:
                        VirtualMouse.LeftClick();
                        break;
                    case ActionType.RightClick:
                        VirtualMouse.RightClick();
                        break;
                    case ActionType.MouseMiddleClick:
                        VirtualMouse.MiddleClick();
                        break;
                    case ActionType.LeftDown:
                        VirtualMouse.LeftDown();
                        break;
                    case ActionType.LeftUp:
                        VirtualMouse.LeftUp();
                        break;
                    case ActionType.RightDown:
                        VirtualMouse.RightDown();
                        break;
                    case ActionType.RightUp:
                        VirtualMouse.RightUp();
                        break;
                    case ActionType.MousWeelScroll:
                        if (request.Arguments.Count < 1) throw new ArgumentException("Scroll requires 'steps' argument.");
                        int steps = int.Parse(request.Arguments[0]);
                        VirtualMouse.ScrollSteps(steps);
                        break;
                    default:
                        throw new NotSupportedException($"Global Action {request.Action} is not supported.");
                }
                return Task.FromResult(new PerformActionResponse { Success = true, Message = "Global action performed." });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Error performing global action: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sends keyboard input using VirtualKeyboard helper.
        /// </summary>
        public Task<PerformActionResponse> SendKeys(SendKeysRequest request, ServerCallContext context)
        {
            try
            {
                VirtualKeyboard.Send(request.Keys, request.Wait);
                return Task.FromResult(new PerformActionResponse { Success = true, Message = "Keys sent" });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PerformActionResponse { Success = false, Message = $"Failed to send keys: {ex.Message}" });
            }
        }

        /// <summary>
        /// Clicks at the center of an element using VirtualMouse.
        /// </summary>
        public static void ClickElementAtCenter(AutomationElement element, bool rightClick = false)
        {
            try { element.SetFocus(); } catch { }

            if (TryGetClickablePoint(element, out Point pt))
            {
                VirtualMouse.MoveTo(pt.X, pt.Y);
                Thread.Sleep(100);
                if (rightClick)
                {
                    VirtualMouse.RightClick();
                }
                else
                {
                    VirtualMouse.LeftClick();
                }
            }
        }

        /// <summary>
        /// Double-clicks at the center of an element using VirtualMouse.
        /// </summary>
        public static void DoubleClickElementAtCenter(AutomationElement element)
        {
            if (TryGetClickablePoint(element, out Point pt))
            {
                VirtualMouse.DoubleClickAt(pt.X, pt.Y);
            }
        }

        /// <summary>
        /// Attempts to get a clickable point for an element.
        /// </summary>
        public static bool TryGetClickablePoint(AutomationElement element, out Point pt)
        {
            if (element.TryGetClickablePoint(out System.Windows.Point winPt)) 
            {
                pt = new Point((int)winPt.X, (int)winPt.Y);
                return true;
            }
            
            // Fallback to center of bounding box
            try {
                var rect = element.Current.BoundingRectangle;
                if (rect.Width > 0 && rect.Height > 0)
                {
                    pt = new Point((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
                    return true;
                }
            } catch {}

            pt = new Point(0, 0);
            return false;
        }
    }
}
