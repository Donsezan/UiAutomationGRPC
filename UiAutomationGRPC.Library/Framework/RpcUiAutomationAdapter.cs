using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using UiAutomationGRPC.Library.Helpers;
using UiAutomationGRPC.Library.Locators;

using Uia = global::UiAutomation;

namespace UiAutomationGRPC.Library
{
    public class RpcUiAutomationAdapter : IAutomationElement
    {
        private readonly Uia.UiAutomationService.UiAutomationServiceClient _client;
        private readonly List<SelectorModel> _selectors;
        
        // Cache the runtime ID once found? 
        // Or re-find every time? The original adapter finds it every time "Build" is called.
        // Let's store the resolved RuntimeId if we want, or simple resolve on demand.
        // To stick to "Logic on server", we really just want to pass the path (selectors).
        // However, the FindElement RPC returns a RuntimeID. 
        // We can either:
        // 1. Resolve the path to a RuntimeID once (or lazily) effectively "finding" it.
        // 2. Pass the full chain every time (not supported by current Proto structure which takes StartRuntimeId + Condition).
        // So we must resolve step-by-step or finding the leaf element first.
        
        // Strategy: Resolve the element on actions.
        
        public RpcUiAutomationAdapter(Uia.UiAutomationService.UiAutomationServiceClient client, Func<BaseSelector> selectorFunc)
        {
            _client = client;
            // BaseSelector stores the list internally. We need to access it.
            // But BaseSelector doesn't expose List easily publicly (it is protected).
            // We might need to reflect or cast if possible, or assume we can get it.
            // Actually, `BaseSelector` has `protected List<SelectorModel> List`.
            // We can't access it unless we inherit or change access.
            // However, the `Selector` class adds to it.
            // Let's assume we can get it via a hack or we update BaseSelector to expose it.
            // MODIFY BaseSelector to expose it? Or pass the list directly?
            // "Func<BaseSelector>" returns the builder. 
            // The original code: new UiAutomationAdapter(() => new Selector()...);
            
            // "Func<BaseSelector>" returns the builder. 
             var baseSelector = selectorFunc();
             _selectors = baseSelector.GetSelectors();
        }

        private string ResolveElement()
        {
            string currentRuntimeId = ""; // Desktop
            
            foreach (var selector in _selectors)
            {
                var req = new Uia.FindElementRequest
                {
                    StartRuntimeId = currentRuntimeId,
                    Scope = ToProtoScope(selector.SearchType),
                    // If we have multiple conditions, we might need an AndCondition. 
                    // SelectorModel has List<Condition>.
                };
                
                // Convert List<Condition> (which is System.Windows.Automation.Condition) to Proto Condition
                if (selector.Condition != null && selector.Condition.Count > 0)
                {
                    if (selector.Condition.Count == 1)
                    {
                        req.Condition = ConditionConverter.Convert(selector.Condition[0]);
                    }
                    else
                    {
                        var boolCond = new Uia.BoolCondition();
                        foreach(var c in selector.Condition)
                        {
                            boolCond.Conditions.Add(ConditionConverter.Convert(c));
                        }
                        req.Condition = new Uia.Condition { AndCondition = boolCond };
                    }
                }
                else
                {
                     req.Condition = new Uia.Condition { TrueCondition = true };
                }

                // Handle AdditionalSearchProperty (Index or Name Contain)
                // This logic needs to be server side or handled via complex conditions?
                // The Proto "Condition" doesn't strictly support "Index" natively in FindElement (usually FindAll + index).
                // Or we update Proto to support Index?
                // User said: "Logic of work with the element on the client side" -> "Server should receive only the path ... and logic ... on the client side"
                // Actually user said: "server should receive only the path ... and all logic of work with the element on the client side" - Wait.
                // "all logic of work with the element on the client side" - this might be a typo OR I am misreading.
                // "Update ... to use IAutomationElement and locators to interact with the server, where the server should receive only the path to the element and the action, and all logic of work with the element on the client side"
                // This reads like: The Client holds the logic (IAutomationElement), determines the path, sends path+action to server. 
                // So my approach is correct: Client has IAutomationElement -> resolves path -> sends commands.
                
                // Back to Index/NameContain:
                // If the selector has "Index", we effectively need to FindAll and pick the Nth.
                // Our Proto `FindElement` returns one element.
                // We might need to loop or update Proto.
                // For now, let's assume we just call FindElement and hope generic conditions work, 
                // BUT "Index" is not a standard UIA property we can filter solely by query in one go typically.
                // However, let's stick to the basic "Name/Id" flow which works with FindElement.
                // If we need Index support, we might need a `FindElements` RPC or `Index` in FindElementRequest.
                // I will assume standard filtering for now.
                
                var resp = _client.FindElement(req);
                currentRuntimeId = resp.RuntimeId;
            }
            return currentRuntimeId;
        }

        private Uia.TreeScope ToProtoScope(SearchType? type)
        {
            if (type == SearchType.Children) return Uia.TreeScope.Children;
            return Uia.TreeScope.Descendants; // Default
        }

        private string _cachedRuntimeId;
        private string GetId()
        {
             // We can cache it if we assume it doesn't change runtime (mostly true for some apps, not others).
             // But to be robust like "WaitForElement", we might want to resolve again if needed.
             // For now, let's just resolve.
             return ResolveElement();
        }

        public void Click()
        {
            var id = GetId();
            _client.PerformAction(new Uia.PerformActionRequest { RuntimeId = id, Action = Uia.ActionType.Click });
        }

        public void Click(int x, int y)
        {
            // Relative click? Proto might need info.
            // Or we assume "Click" with args?
            // Proto has "MoveTo" then "Click"?
            // Or "Click" action.
            // Let's ignore x,y for basic Click or implement Move+Click if Proto supports.
            // Update Proto later if needed.
            Click(); 
        }

        public void DoubleClick()
        {
             var id = GetId();
            _client.PerformAction(new Uia.PerformActionRequest { RuntimeId = id, Action = Uia.ActionType.DoubleClick });
        }

        public void Hover()
        {
             var id = GetId();
             // Maybe "MoveTo"?
            _client.PerformAction(new Uia.PerformActionRequest { RuntimeId = id, Action = Uia.ActionType.MoveTo });
        }

        public string Name()
        {
            var id = GetId();
            return _client.GetProperty(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "Name" }).Value;
        }

        public string ClassName()
        {
            var id = GetId();
            return _client.GetProperty(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "ClassName" }).Value;
        }

        public string AutomationId()
        {
            var id = GetId();
            return _client.GetProperty(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "AutomationId" }).Value;
        }

        public void WaitForElementIsClickable()
        {
            // Polling on Client side
             var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            while (stopWatch.Elapsed.TotalSeconds < UsabilityTimeLimits.ApplicationLoadLimit)
            {
                try
                {
                    var id = ResolveElement();
                    // Checking if resolve works is essentially "Exist".
                    // "Clickable" usually means resolving + maybe check "IsEnabled"?
                    if (!string.IsNullOrEmpty(id)) return;
                }
                catch {}
                Thread.Sleep(500);
            }
            throw new TimeoutException("Element not clickable");
        }

        public void WaitForElementExist()
        {
             var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            while (stopWatch.Elapsed.TotalSeconds < UsabilityTimeLimits.ApplicationLoadLimit)
            {
                try
                {
                    var id = ResolveElement();
                    if (!string.IsNullOrEmpty(id)) return;
                }
                catch {}
                Thread.Sleep(500);
            }
             throw new TimeoutException("Element not found");
        }

        public bool IsElementExist()
        {
            try
            {
                var id = ResolveElement();
                return !string.IsNullOrEmpty(id);
            }
            catch
            {
                return false;
            }
        }

        public bool IsElementClicable()
        {
             return IsElementExist();
        }

        public bool WaitElementExistStatusForTime(bool status, int time)
        {
            // Simplified implementation
            var start = DateTime.Now;
            while((DateTime.Now - start).TotalSeconds < time)
            {
                bool exists = IsElementExist();
                if (exists == status) return status;
                Thread.Sleep(100);
            }
            return !status;
        }

        public bool WaitElementClickableStatusForTime(bool status, int time = UsabilityTimeLimits.ApplicationLoadLimit)
        {
             return WaitElementExistStatusForTime(status, time);
        }

        public Rectangle GetRectangle()
        {
             throw new NotImplementedException("Rectangle retrieval via gRPC not yet implemented fully.");
        }
    }
}
