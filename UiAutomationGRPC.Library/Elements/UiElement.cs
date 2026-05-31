using System.Drawing;
using Grpc.Core;
using UiAutomationGRPC.Library.Helpers;
using UiAutomationGRPC.Library.Selectors;
using Uia = global::UiAutomation;

namespace UiAutomationGRPC.Library.Elements
{
    /// <summary>
    /// Represents a UI element that interacts with the UI Automation gRPC service.
    /// All public methods are async-only because every operation involves a gRPC network
    /// call to the server. Async prevents thread-blocking during network I/O and avoids
    /// deadlocks when consumed from UI threads or synchronization-context-bound callers.
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

        #region Element Resolution

        /// <summary>
        /// Resolves the runtime ID of the element by walking the selector chain.
        /// Validates each step and throws descriptive exceptions on failure.
        /// </summary>
        private async Task<string> ResolveElementAsync()
        {
            string currentRuntimeId = ""; // Desktop

            foreach (var selector in _selectors)
            {
                var req = BuildFindRequest(currentRuntimeId, selector);
                var resp = await _client.FindElementAsync(req);

                ValidateRuntimeId(resp.RuntimeId, selector, resp.Message);
                currentRuntimeId = resp.RuntimeId;
            }
            return currentRuntimeId;
        }

        /// <summary>
        /// Builds a <see cref="Uia.FindElementRequest"/> from a selector model.
        /// </summary>
        private static Uia.FindElementRequest BuildFindRequest(string startRuntimeId, SelectorModel selector)
        {
            var req = new Uia.FindElementRequest
            {
                StartRuntimeId = startRuntimeId,
                Scope = ToProtoScope(selector.SearchType),
            };

            if (selector.Condition is { Count: > 0 })
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

            return req;
        }

        /// <summary>
        /// Validates that a RuntimeId is non-empty after a FindElement call.
        /// Throws <see cref="InvalidOperationException"/> with selector detail on failure.
        /// The server reports not-found / blocked as { Success = false, Message } with an empty
        /// RuntimeId (rather than an RpcException), so <paramref name="serverMessage"/> carries
        /// the server-side reason when available.
        /// </summary>
        private static void ValidateRuntimeId(string runtimeId, SelectorModel selector, string? serverMessage = null)
        {
            if (!string.IsNullOrEmpty(runtimeId))
                return;

            var conditionDescription = BuildConditionDescription(selector);
            var serverDetail = string.IsNullOrEmpty(serverMessage) ? "" : $" Server: {serverMessage}";
            throw new InvalidOperationException(
                $"FindElement returned an empty RuntimeId. " +
                $"Selector conditions: [{conditionDescription}], " +
                $"SearchType: {selector.SearchType?.ToString() ?? "null"}. " +
                "The element was not found in the UI tree." + serverDetail);
        }

        /// <summary>
        /// Builds a human-readable description of selector conditions for error diagnostics.
        /// </summary>
        private static string BuildConditionDescription(SelectorModel selector)
        {
            if (selector.Condition is null || selector.Condition.Count == 0)
                return "TrueCondition (match all)";

            var parts = new List<string>();
            foreach (var cond in selector.Condition)
            {
                if (cond.ConditionTypeCase == Uia.Condition.ConditionTypeOneofCase.PropertyCondition)
                {
                    var pc = cond.PropertyCondition;
                    parts.Add($"{pc.PropertyName}='{pc.PropertyValue}'");
                }
                else
                {
                    parts.Add(cond.ConditionTypeCase.ToString());
                }
            }
            return string.Join(", ", parts);
        }

        private static Uia.TreeScope ToProtoScope(SearchType? type)
        {
            if (type == SearchType.Children) return Uia.TreeScope.Children;
            return Uia.TreeScope.Descendants; // Default
        }

        #endregion

        #region Actions

        /// <inheritdoc />
        public async Task ClickAsync()
        {
            var id = await ResolveElementAsync();
            await _client.PerformActionAsync(new Uia.PerformActionRequest { RuntimeId = id, Action = Uia.ActionType.Click });
        }

        /// <inheritdoc />
        public async Task ClickAsync(int x, int y)
        {
            // Move cursor to absolute screen coordinates, then left-click (global mouse actions)
            await _client.PerformActionAsync(new Uia.PerformActionRequest
            {
                RuntimeId = "",
                Action = Uia.ActionType.Move,
                Arguments = { x.ToString(), y.ToString() }
            });
            await _client.PerformActionAsync(new Uia.PerformActionRequest
            {
                RuntimeId = "",
                Action = Uia.ActionType.LeftClick
            });
        }

        /// <inheritdoc />
        public async Task DoubleClickAsync()
        {
            var id = await ResolveElementAsync();
            await _client.PerformActionAsync(new Uia.PerformActionRequest { RuntimeId = id, Action = Uia.ActionType.DoubleClick });
        }

        /// <inheritdoc />
        public async Task HoverAsync()
        {
            var id = await ResolveElementAsync();
            await _client.PerformActionAsync(new Uia.PerformActionRequest { RuntimeId = id, Action = Uia.ActionType.MoveTo });
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        public async Task<string> NameAsync()
        {
            var id = await ResolveElementAsync();
            var resp = await _client.GetPropertyAsync(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "Name" });
            return resp.Value;
        }

        /// <inheritdoc />
        public async Task<string> ClassNameAsync()
        {
            var id = await ResolveElementAsync();
            var resp = await _client.GetPropertyAsync(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "ClassName" });
            return resp.Value;
        }

        /// <inheritdoc />
        public async Task<string> AutomationIdAsync()
        {
            var id = await ResolveElementAsync();
            var resp = await _client.GetPropertyAsync(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "AutomationId" });
            return resp.Value;
        }

        /// <inheritdoc />
        public async Task<Rectangle> GetRectangleAsync()
        {
            var id = await ResolveElementAsync();
            var resp = await _client.GetPropertyAsync(new Uia.GetPropertyRequest { RuntimeId = id, PropertyName = "BoundingRectangle" });

            if (!resp.Success)
                throw new Exception($"Failed to get rectangle: {resp.Message}");

            return ParseRectangle(resp.Value);
        }

        /// <inheritdoc />
        public async Task<string> GetRuntimeIdAsync()
        {
            return await ResolveElementAsync();
        }

        #endregion

        #region Waiting & Existence

        /// <inheritdoc />
        public async Task WaitForElementIsClickableAsync()
        {
            await WaitForElementAsync("Element not clickable within timeout");
        }

        /// <inheritdoc />
        public async Task WaitForElementExistAsync()
        {
            await WaitForElementAsync("Element not found within timeout");
        }

        /// <inheritdoc />
        public async Task<bool> IsElementExistAsync()
        {
            try
            {
                var id = await ResolveElementAsync();
                return !string.IsNullOrEmpty(id);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                // Empty RuntimeId — element was not found
                return false;
            }
            // Auth, network, and server errors propagate
        }

        /// <inheritdoc />
        public async Task<bool> IsElementClickableAsync()
        {
            return await IsElementExistAsync();
        }

        /// <inheritdoc />
        public async Task<bool> WaitElementExistStatusForTimeAsync(bool status, int time)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < time)
            {
                bool exists = await IsElementExistAsync();
                if (exists == status) return status;
                await Task.Delay(100);
            }
            return !status;
        }

        /// <inheritdoc />
        public async Task<bool> WaitElementClickableStatusForTimeAsync(bool status, int time = UsabilityTimeLimits.ApplicationLoadLimit)
        {
            return await WaitElementExistStatusForTimeAsync(status, time);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Waits for the element to become available.
        /// Only swallows NotFound and empty-RuntimeId; other errors propagate immediately.
        /// </summary>
        private async Task WaitForElementAsync(string timeoutMessage)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            while (stopWatch.Elapsed.TotalSeconds < UsabilityTimeLimits.ApplicationLoadLimit)
            {
                try
                {
                    var id = await ResolveElementAsync();
                    if (!string.IsNullOrEmpty(id)) return;
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
                {
                    // Element not yet available — retry
                }
                catch (InvalidOperationException)
                {
                    // Empty RuntimeId — element not yet available — retry
                }
                // Auth, network, or server errors propagate immediately
                await Task.Delay(500);
            }
            throw new TimeoutException(timeoutMessage);
        }

        /// <summary>
        /// Parses a "X,Y,Width,Height" string into a <see cref="Rectangle"/>.
        /// </summary>
        private static Rectangle ParseRectangle(string value)
        {
            var parts = value.Split(new[] { ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 &&
                double.TryParse(parts[0], out double x) &&
                double.TryParse(parts[1], out double y) &&
                double.TryParse(parts[2], out double w) &&
                double.TryParse(parts[3], out double h))
            {
                return new Rectangle((int)x, (int)y, (int)w, (int)h);
            }
            throw new FormatException($"Failed to parse rectangle from '{value}'. Expected format: 'X,Y,Width,Height'.");
        }

        #endregion
    }
}
