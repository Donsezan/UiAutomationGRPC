using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using UiAutomationGRPC.Library.Helpers;
using UiAutomationGRPC.Library.Selectors;
using Uia = global::UiAutomation;

namespace UiAutomationGRPC.Library.Elements
{
    /// <summary>
    /// Represents a UI element that interacts with the UI Automation gRPC service.
    /// </summary>
    public class UiElement : IAutomationElement
    {
        private readonly Uia.UiAutomationService.UiAutomationServiceClient _client;
        private readonly List<SelectorModel> _selectors;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="UiElement"/> class.
        /// </summary>
        /// <param name="driver">The UI automation driver.</param>
        /// <param name="selectorFunc">The selector builder function.</param>
        public UiElement(UiAutomationDriver driver, Func<BaseSelector> selectorFunc)
        {
            _client = driver.Client;
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
                };
                
                if (selector.Condition != null && selector.Condition.Count > 0)
                {
                    if (selector.Condition.Count == 1)
                    {
                        req.Condition = selector.Condition[0];
                    }
                    else
                    {
                        var boolCond = new Uia.BoolCondition();
                        boolCond.Conditions.AddRange(selector.Condition);
                        req.Condition = new Uia.Condition { AndCondition = boolCond };
                    }
                }
                else
                {
                     req.Condition = new Uia.Condition { TrueCondition = true };
                }

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

        private string GetId()
        {
             return ResolveElement();
        }

        /// <inheritdoc />
        public void Click()
        {
            var id = GetId();
            _client.PerformAction(new Uia.PerformActionRequest { RuntimeId = id, Action = Uia.ActionType.Click });
        }

        /// <inheritdoc />
        public void Click(int x, int y)
        {
            Click(); 
        }

        /// <inheritdoc />
        public void DoubleClick()
        {
             var id = GetId();
            _client.PerformAction(new Uia.PerformActionRequest { RuntimeId = id, Action = Uia.ActionType.DoubleClick });
        }

        /// <inheritdoc />
        public void Hover()
        {
             var id = GetId();
            _client.PerformAction(new Uia.PerformActionRequest { RuntimeId = id, Action = Uia.ActionType.MoveTo });
        }

        /// <inheritdoc />
        public string Name()
        {
            var id = GetId();
            return _client.GetProperty(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "Name" }).Value;
        }

        /// <inheritdoc />
        public string ClassName()
        {
            var id = GetId();
            return _client.GetProperty(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "ClassName" }).Value;
        }

        /// <inheritdoc />
        public string AutomationId()
        {
            var id = GetId();
            return _client.GetProperty(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "AutomationId" }).Value;
        }

        /// <inheritdoc />
        public void WaitForElementIsClickable()
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
            throw new TimeoutException("Element not clickable");
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public bool IsElementClicable()
        {
             return IsElementExist();
        }

        /// <inheritdoc />
        public bool WaitElementExistStatusForTime(bool status, int time)
        {
            var start = DateTime.Now;
            while((DateTime.Now - start).TotalSeconds < time)
            {
                bool exists = IsElementExist();
                if (exists == status) return status;
                Thread.Sleep(100);
            }
            return !status;
        }

        /// <inheritdoc />
        public bool WaitElementClickableStatusForTime(bool status, int time = UsabilityTimeLimits.ApplicationLoadLimit)
        {
             return WaitElementExistStatusForTime(status, time);
        }

        /// <inheritdoc />
        public Rectangle GetRectangle()
        {
             throw new NotImplementedException("Rectangle retrieval via gRPC not yet implemented fully.");
        }

        /// <inheritdoc />
        public string GetRuntimeId()
        {
            return GetId();
        }
    }
}
