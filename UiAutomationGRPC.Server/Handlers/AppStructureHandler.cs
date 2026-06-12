using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;
using PropertyCondition = System.Windows.Automation.PropertyCondition;
using UiaCondition = System.Windows.Automation.Condition;
using UiaTreeScope = System.Windows.Automation.TreeScope;

namespace UiAutomationGRPC.Server.Handlers
{
    /// <summary>
    /// Handles app structure operations (LLM-friendly layer).
    /// Responsible only for reading UI structure — does NOT launch applications.
    /// </summary>
    public class AppStructureHandler
    {
        private readonly ActionHandler _actionHandler;
        private readonly ILogger<AppStructureHandler> _logger;
        private readonly InteractionAccessGuard? _guard;
        private readonly AppStructureOptions _options;
        private readonly JsonSerializerSettings _jsonSettings;

        public AppStructureHandler(
            ILogger<AppStructureHandler> logger,
            ActionHandler? actionHandler = null,
            InteractionAccessGuard? guard = null,
            AppStructureOptions? options = null)
        {
            _logger = logger;
            _actionHandler = actionHandler ?? new ActionHandler();
            _guard = guard;
            _options = options ?? new AppStructureOptions();
            _jsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        }

        public AppStructureResponse GetAppStructure(AppStructureRequest request, ServerCallContext context)
        {
            try
            {
                var effective = _options.MergeWith(request.StructureOptions);

                // Scoped build: the client asked for the subtree under a known element instead of
                // the whole window — much cheaper for big/dynamic apps.
                string scopeId = request.StructureOptions?.ScopeRuntimeId ?? "";
                if (!string.IsNullOrEmpty(scopeId))
                {
                    if (!ElementCache.TryGetLive(scopeId, out var scopeElement))
                        return new AppStructureResponse { Success = false, Message = $"Scope element '{scopeId}' not found in cache." };

                    var scopeBlocked = InteractionAccessGuard.CheckAccess(_guard, scopeElement.Current.ProcessId);
                    if (scopeBlocked != null)
                        return new AppStructureResponse { Success = false, Message = scopeBlocked };

                    var (scopedNode, scopedCtx) = BuildTree(scopeElement, context.CancellationToken, effective);
                    return new AppStructureResponse
                    {
                        Success = true,
                        JsonStructure = Serialize(scopedNode, effective),
                        Message = DescribeResult(scopedCtx)
                    };
                }

                Process[] processes = null;

                if (request.UseProcessId && request.ProcessId > 0)
                {
                    var p = Process.GetProcessById(request.ProcessId);
                    if (p != null) processes = new[] { p };
                }
                else if (!string.IsNullOrEmpty(request.AppName))
                {
                    // Strip .exe if present
                    string appName = ElementCache.StripExeExtension(request.AppName);
                    processes = Process.GetProcessesByName(appName);
                }

                // If application is not running, return an error — this method
                // is responsible only for reading structure, not launching apps.
                if (processes == null || processes.Length == 0)
                {
                    return new AppStructureResponse
                    {
                        Success = false,
                        Message = $"Application is not running: '{request.AppName ?? $"PID {request.ProcessId}"}'. Launch the application first using OpenApp."
                    };
                }

                AutomationElement rootMapElement = null;
                // A non-Window top-level element (Pane/Menu) seen while searching. The MainWindowHandle
                // heuristic can hand back a transient dropdown popup instead of the real window, so we
                // only accept a real Window here and keep this as a last-resort fallback (see below).
                AutomationElement popupFallback = null;

                foreach (var p in processes)
                {
                    try
                    {
                        p.Refresh();
                        if (p.HasExited) continue;

                        // Strategy 1: MainWindowHandle — accept only a real Window control type.
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            try
                            {
                                var candidate = AutomationElement.FromHandle(p.MainWindowHandle);
                                if (candidate != null && !IsUwpSpacer(candidate))
                                {
                                    if (IsWindowControlType(candidate))
                                    {
                                        rootMapElement = candidate;
                                        break;
                                    }
                                    // Popup/menu/pane grabbed by the heuristic — remember, keep looking.
                                    popupFallback ??= candidate;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Failed to get element from MainWindowHandle for process {ProcessId}", p.Id);
                            }
                        }

                        // Strategy 2: Search this PID's top-level elements, preferring a real Window.
                        if (rootMapElement == null)
                        {
                            var condition = new PropertyCondition(AutomationElement.ProcessIdProperty, p.Id);
                            try
                            {
                                var candidates = AutomationElement.RootElement.FindAll(UiaTreeScope.Children, condition);
                                AutomationElement firstAny = null;
                                foreach (AutomationElement c in candidates)
                                {
                                    firstAny ??= c;
                                    if (IsWindowControlType(c))
                                    {
                                        rootMapElement = c;
                                        break;
                                    }
                                }
                                if (rootMapElement != null) break;
                                // No real Window for this PID — keep the first top-level as a fallback.
                                if (firstAny != null) popupFallback ??= firstAny;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Failed to find element by ProcessId {ProcessId}", p.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error processing process {ProcessId}", p.Id);
                    }
                }

                // Strategy 3: Fallback to Window Name search
                if (rootMapElement == null)
                {
                    string nameToSearch = !string.IsNullOrEmpty(request.AppName)
                        ? ElementCache.StripExeExtension(request.AppName)
                        : request.AppName;

                    try
                    {
                        var nameCondition = new PropertyCondition(AutomationElement.NameProperty, nameToSearch);
                        var candidate = AutomationElement.RootElement.FindFirst(UiaTreeScope.Children, nameCondition);
                        if (candidate != null)
                        {
                            rootMapElement = candidate;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to find window by name '{AppName}'", request.AppName);
                    }
                }

                // Safety net: no real top-level Window matched. If a non-Window candidate was seen
                // (a popup-only process, a context-menu host, or an app whose real window is a Pane,
                // e.g. Electron/Chromium shells), return it rather than failing — that is legitimately
                // all this process exposes.
                if (rootMapElement == null && popupFallback != null)
                    rootMapElement = popupFallback;

                if (rootMapElement == null)
                    return new AppStructureResponse { Success = false, Message = "Main window element not found." };

                // Validate interaction access against the owning process
                var blocked = InteractionAccessGuard.CheckAccess(_guard, rootMapElement.Current.ProcessId);
                if (blocked != null)
                    return new AppStructureResponse { Success = false, Message = blocked };

                // Flush stale cache for this process before rebuilding fresh
                try { ElementCache.ClearByProcess(rootMapElement.Current.ProcessId); }
                catch (System.Windows.Automation.ElementNotAvailableException) { }

                var (rootNode, ctx) = BuildTree(rootMapElement, context.CancellationToken, effective);
                var json = Serialize(rootNode, effective);

                return new AppStructureResponse { Success = true, JsonStructure = json, Message = DescribeResult(ctx) };
            }
            catch (OperationCanceledException)
            {
                throw new RpcException(new Status(StatusCode.Cancelled, "GetAppStructure cancelled by client."));
            }
            catch (Exception ex)
            {
                return new AppStructureResponse { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        public AppStructureResponse PerformActionWithStructure(PerformActionRequest request, ServerCallContext context)
        {
            var actionResult = _actionHandler.PerformAction(request, context);
            if (!actionResult.Success)
            {
                return new AppStructureResponse { Success = false, Message = actionResult.Message };
            }

            try
            {
                var effective = _options.MergeWith(request.StructureOptions);

                if (ElementCache.TryGetLive(request.RuntimeId, out var element))
                {
                    var window = ScreenshotHandler.GetTopLevelWindow(element);
                    if (window != null)
                    {
                        // Let the UI settle after the action (event-driven quiescence with a hard
                        // cap for continuously-updating apps). We hold the worker for this window
                        // intentionally so no other operation interleaves before the refreshed read.
                        UiSettler.WaitForQuiet(window, effective.SettleQuietMs, effective.SettleMaxMs);

                        // Scoped rebuild: return only the subtree the client cares about.
                        // Resolve AFTER the action + settle so the scope element reflects the new state.
                        AutomationElement rebuildRoot = window;
                        string scopeId = request.StructureOptions?.ScopeRuntimeId ?? "";
                        if (!string.IsNullOrEmpty(scopeId) && ElementCache.TryGetLive(scopeId, out var scopeElement))
                            rebuildRoot = scopeElement;

                        // Flush stale cache for this process before rebuilding fresh
                        try { ElementCache.ClearByProcess(window.Current.ProcessId); }
                        catch (System.Windows.Automation.ElementNotAvailableException) { }

                        var (rootNode, ctx) = BuildTree(rebuildRoot, context.CancellationToken, effective);
                        var json = Serialize(rootNode, effective);
                        return new AppStructureResponse { Success = true, JsonStructure = json, Message = $"Action performed. {DescribeResult(ctx)}" };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new RpcException(new Status(StatusCode.Cancelled, "PerformActionWithStructure cancelled by client."));
            }
            catch (Exception ex) when (ex is ElementNotAvailableException || ex is COMException)
            {
                // The action itself already succeeded (we only reach here after actionResult.Success).
                // The target element/window then went away before we could read the refreshed tree —
                // e.g. invoking a window's Close button. That is a successful action, not a failure:
                // report success with a null tree instead of surfacing the COM/UIA exception as an RPC error.
                return new AppStructureResponse { Success = true, Message = "Action succeeded; process exited during response." };
            }

            return new AppStructureResponse { Success = true, Message = "Action performed but could not rebuild structure (root not found)." };
        }

        /// <summary>
        /// Builds the LLM-facing tree for <paramref name="liveRoot"/>. Uses a single
        /// <see cref="CacheRequest"/> over the subtree so per-node property reads are served from
        /// the cached snapshot instead of one cross-process COM round-trip each. Falls back to live
        /// reads transparently if the cache cannot be activated.
        /// </summary>
        private (AppNode node, BuildContext ctx) BuildTree(AutomationElement liveRoot, CancellationToken ct, AppStructureOptions? options = null)
        {
            var ctx = new BuildContext { Ct = ct, Options = options ?? _options };
            AutomationElement root = liveRoot;

            try
            {
                using (BuildCacheRequest().Activate())
                {
                    // Re-fetch the root inside the active request so it — and, with TreeScope.Subtree,
                    // its whole subtree — carries cached property values reachable via CachedChildren.
                    var cached = liveRoot.FindFirst(UiaTreeScope.Element, UiaCondition.TrueCondition);
                    if (cached != null) root = cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "CacheRequest activation failed; falling back to live property reads.");
            }

            var node = BuildNode(root, ctx, depth: 0);
            return (node, ctx);
        }

        /// <summary>Prefetches every property the tree builder reads, across the whole subtree.</summary>
        private static CacheRequest BuildCacheRequest()
        {
            var cr = new CacheRequest
            {
                TreeScope = UiaTreeScope.Subtree,
                TreeFilter = Automation.ControlViewCondition,
                AutomationElementMode = AutomationElementMode.Full // keep live refs usable for later interaction/re-find
            };
            cr.Add(AutomationElement.RuntimeIdProperty);
            cr.Add(AutomationElement.AutomationIdProperty);
            cr.Add(AutomationElement.NameProperty);
            cr.Add(AutomationElement.ClassNameProperty);
            cr.Add(AutomationElement.ControlTypeProperty);
            cr.Add(AutomationElement.ProcessIdProperty);
            cr.Add(AutomationElement.IsOffscreenProperty);
            cr.Add(AutomationElement.BoundingRectangleProperty);
            cr.Add(AutomationElement.IsInvokePatternAvailableProperty);
            cr.Add(AutomationElement.IsTogglePatternAvailableProperty);
            cr.Add(AutomationElement.IsExpandCollapsePatternAvailableProperty);
            cr.Add(AutomationElement.IsSelectionItemPatternAvailableProperty);
            cr.Add(AutomationElement.IsValuePatternAvailableProperty);
            cr.Add(ValuePattern.IsReadOnlyProperty);
            return cr;
        }

        private AppNode BuildNode(AutomationElement element, BuildContext ctx, int depth)
        {
            ctx.Ct.ThrowIfCancellationRequested();
            var options = ctx.Options;

            if (options.MaxNodes > 0 && ctx.Emitted >= options.MaxNodes)
            {
                ctx.Truncated = true;
                return null;
            }

            bool isRoot = depth == 0;

            bool isOffscreen = AsBool(Prop(element, AutomationElement.IsOffscreenProperty));

            Rect rect;
            try { rect = AsRect(Prop(element, AutomationElement.BoundingRectangleProperty)); }
            catch { rect = Rect.Empty; }
            bool hasRect = !rect.IsEmpty;
            bool zeroSize = hasRect && rect.Width <= 0 && rect.Height <= 0;

            // Filter offscreen / zero-size nodes (and their subtrees) unless opted in.
            // The root window is never filtered, so a minimized app still returns a tree.
            if (!isRoot && !options.IncludeOffscreen && (isOffscreen || zeroSize))
                return null;

            string automationId = AsString(Prop(element, AutomationElement.AutomationIdProperty));
            string name = AsString(Prop(element, AutomationElement.NameProperty));
            string className = AsString(Prop(element, AutomationElement.ClassNameProperty));
            var controlType = Prop(element, AutomationElement.ControlTypeProperty) as ControlType;
            string controlTypeName = controlType?.ProgrammaticName ?? "";
            int processId = AsInt(Prop(element, AutomationElement.ProcessIdProperty));

            // An element is "clickable" if it exposes any pattern an agent can act on:
            // Invoke (buttons), Toggle (checkboxes), ExpandCollapse (tree/combo nodes),
            // SelectionItem (list/tab/radio items), a settable Value (editable fields), or it is a
            // Hyperlink control. The earlier invoke||toggle-only check missed most of these.
            bool invoke = AsBool(Prop(element, AutomationElement.IsInvokePatternAvailableProperty));
            bool toggle = AsBool(Prop(element, AutomationElement.IsTogglePatternAvailableProperty));
            bool expandCollapse = AsBool(Prop(element, AutomationElement.IsExpandCollapsePatternAvailableProperty));
            bool selectionItem = AsBool(Prop(element, AutomationElement.IsSelectionItemPatternAvailableProperty));
            bool valueSettable = AsBool(Prop(element, AutomationElement.IsValuePatternAvailableProperty))
                                 && !AsBool(Prop(element, ValuePattern.IsReadOnlyProperty));
            bool isHyperlink = controlType == ControlType.Hyperlink;
            bool clickable = invoke || toggle || expandCollapse || selectionItem || valueSettable || isHyperlink;

            string runtimeId = (Prop(element, AutomationElement.RuntimeIdProperty) is int[] rid)
                ? string.Join(",", rid)
                : string.Join(",", element.GetRuntimeId());

            ElementCache.CacheElement(element, runtimeId, automationId, name, className, controlTypeName, processId);
            ctx.Emitted++;

            var node = new AppNode
            {
                UniqId = runtimeId,
                UiAutomationId = automationId,
                Name = name,
                ControlType = controlTypeName,
                IsClickable = clickable,
                IsVisible = !isOffscreen,
                BoundingRectangle = hasRect ? $"{(int)rect.Left},{(int)rect.Top},{(int)rect.Width},{(int)rect.Height}" : null
            };

            var children = GetChildElements(element);

            // Depth cap: stop descending but record that this subtree is incomplete.
            if (options.MaxDepth > 0 && depth >= options.MaxDepth)
            {
                if (children.Count > 0)
                {
                    node.ChildrenTruncated = true;
                    ctx.Truncated = true;
                }
                return node;
            }

            foreach (var child in children)
            {
                var childNode = BuildNode(child, ctx, depth + 1);
                if (childNode != null)
                    node.Children.Add(childNode);

                if (options.MaxNodes > 0 && ctx.Emitted >= options.MaxNodes)
                {
                    node.ChildrenTruncated = true;
                    ctx.Truncated = true;
                    break;
                }
            }

            return node;
        }

        /// <summary>
        /// Child elements via the cached snapshot (no COM round-trips) when available,
        /// falling back to a live ControlView TreeWalker if this element was not cached.
        /// </summary>
        private static List<AutomationElement> GetChildElements(AutomationElement element)
        {
            try
            {
                var cached = element.CachedChildren;
                var list = new List<AutomationElement>(cached.Count);
                foreach (AutomationElement c in cached)
                    list.Add(c);
                return list;
            }
            catch (InvalidOperationException)
            {
                // Element has no cached children (cache not active / not requested) — read live.
                return ElementHandler.GetChildElements(element);
            }
        }

        private string Serialize(AppNode root, AppStructureOptions? options = null) =>
            JsonConvert.SerializeObject(root, (options ?? _options).CompactJson ? Formatting.None : Formatting.Indented, _jsonSettings);

        private static string DescribeResult(BuildContext ctx) =>
            ctx.Truncated
                ? $"Structure retrieved ({ctx.Emitted} nodes, truncated — raise Features:AppStructure MaxDepth/MaxNodes to see more)."
                : $"Structure retrieved ({ctx.Emitted} nodes).";

        // --- cached-value readers: prefer the CacheRequest snapshot, fall back to a live read ---

        private static object Prop(AutomationElement element, AutomationProperty property)
        {
            try { return element.GetCachedPropertyValue(property); }
            catch (InvalidOperationException) { return element.GetCurrentPropertyValue(property); }
        }

        private static bool AsBool(object o) => o is bool b && b;
        private static string AsString(object o) => o as string ?? "";
        private static int AsInt(object o) => o is int i ? i : 0;
        private static Rect AsRect(object o) => o is Rect r ? r : Rect.Empty;

        private sealed class BuildContext
        {
            public CancellationToken Ct;
            public AppStructureOptions Options = new();
            public int Emitted;
            public bool Truncated;
        }

        /// <summary>
        /// True when the element's control type is <see cref="ControlType.Window"/> — i.e. a real
        /// top-level application window rather than a transient popup/menu/pane.
        /// </summary>
        private static bool IsWindowControlType(AutomationElement element)
        {
            try
            {
                return Equals(element.Current.ControlType, ControlType.Window);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[AppStructureHandler] ControlType check failed: {ex.Message}");
                return false;
            }
        }

        private static bool IsUwpSpacer(AutomationElement element)
        {
            try
            {
                return !HasChildren(element);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[AppStructureHandler] IsUwpSpacer check failed (assuming spacer): {ex.Message}");
                return true;
            }
        }

        private static bool HasChildren(AutomationElement element)
        {
            var walker = TreeWalker.ControlViewWalker;
            return walker.GetFirstChild(element) != null;
        }
    }
}
