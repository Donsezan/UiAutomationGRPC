using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;
using UiAutomationGRPC.Server.Models;
using PropertyCondition = FlaUI.Core.Conditions.PropertyCondition;
using UiaTreeScope = FlaUI.Core.Definitions.TreeScope;

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

                    int scopePid = scopeElement.Properties.ProcessId.ValueOrDefault;
                    var scopeBlocked = InteractionAccessGuard.CheckAccess(_guard, scopePid);
                    if (scopeBlocked != null)
                        return new AppStructureResponse { Success = false, Message = scopeBlocked };

                    var (scopedNode, scopedCtx) = BuildTree(scopeElement, context.CancellationToken, effective);
                    var scopedJson = SerializeWithDiff(scopedNode, scopePid,
                        request.StructureOptions?.DiffMode == true, effective, out var scopedNote);
                    return new AppStructureResponse
                    {
                        Success = true,
                        JsonStructure = scopedJson,
                        Message = DescribeResult(scopedCtx) + scopedNote
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
                                var candidate = UiaRuntime.Automation.FromHandle(p.MainWindowHandle);
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
                            var condition = new PropertyCondition(UiaRuntime.Properties.Element.ProcessId, p.Id);
                            try
                            {
                                var candidates = UiaRuntime.Desktop.FindAll(UiaTreeScope.Children, condition);
                                AutomationElement firstAny = null;
                                foreach (var c in candidates)
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
                        var nameCondition = new PropertyCondition(UiaRuntime.Properties.Element.Name, nameToSearch);
                        var candidate = UiaRuntime.Desktop.FindFirst(UiaTreeScope.Children, nameCondition);
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

                int rootPid = rootMapElement.Properties.ProcessId.ValueOrDefault;

                // Validate interaction access against the owning process
                var blocked = InteractionAccessGuard.CheckAccess(_guard, rootPid);
                if (blocked != null)
                    return new AppStructureResponse { Success = false, Message = blocked };

                // Flush stale cache for this process before rebuilding fresh
                ElementCache.ClearByProcess(rootPid);

                var (rootNode, ctx) = BuildTree(rootMapElement, context.CancellationToken, effective);
                var json = SerializeWithDiff(rootNode, rootPid,
                    request.StructureOptions?.DiffMode == true, effective, out var note);

                return new AppStructureResponse { Success = true, JsonStructure = json, Message = DescribeResult(ctx) + note };
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

                        int windowPid = window.Properties.ProcessId.ValueOrDefault;

                        // Flush stale cache for this process before rebuilding fresh
                        ElementCache.ClearByProcess(windowPid);

                        var (rootNode, ctx) = BuildTree(rebuildRoot, context.CancellationToken, effective);
                        var json = SerializeWithDiff(rootNode, windowPid,
                            request.StructureOptions?.DiffMode == true, effective, out var note);
                        return new AppStructureResponse { Success = true, JsonStructure = json, Message = $"Action performed. {DescribeResult(ctx)}{note}" };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new RpcException(new Status(StatusCode.Cancelled, "PerformActionWithStructure cancelled by client."));
            }
            catch (Exception ex) when (UiaRuntime.IsStaleElement(ex) || ex is COMException)
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
        /// Builds the LLM-facing tree for <paramref name="liveRoot"/>. A <see cref="CacheRequest"/>
        /// is held active over the whole build so the subtree fetch and all per-node property reads
        /// come from one batched snapshot instead of one cross-process COM round-trip each
        /// (FlaUI routes reads to the cache while an activation is in scope). Falls back to live
        /// reads transparently if the cached build fails.
        /// </summary>
        private (AppNode node, BuildContext ctx) BuildTree(AutomationElement liveRoot, CancellationToken ct, AppStructureOptions? options = null)
        {
            try
            {
                var ctx = new BuildContext { Ct = ct, Options = options ?? _options, Cached = true };
                using (BuildCacheRequest().Activate())
                {
                    // Re-fetch the root inside the active request so it — and, with TreeScope.Subtree,
                    // its whole subtree — carries cached property values reachable via CachedChildren.
                    var cachedRoot = liveRoot.FindFirst(UiaTreeScope.Element, TrueCondition.Default);
                    if (cachedRoot != null)
                    {
                        var node = BuildNode(cachedRoot, ctx, depth: 0);
                        return (node, ctx);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Cached tree build failed; falling back to live property reads.");
            }

            var liveCtx = new BuildContext { Ct = ct, Options = options ?? _options, Cached = false };
            var liveNode = BuildNode(liveRoot, liveCtx, depth: 0);
            return (liveNode, liveCtx);
        }

        /// <summary>Prefetches every property the tree builder reads, across the whole subtree.</summary>
        private static CacheRequest BuildCacheRequest()
        {
            var lib = UiaRuntime.Properties;
            var cr = new CacheRequest
            {
                TreeScope = UiaTreeScope.Subtree,
                // Control view only — same filter the live TreeWalker path uses.
                TreeFilter = new PropertyCondition(lib.Element.IsControlElement, true),
                AutomationElementMode = AutomationElementMode.Full // keep live refs usable for later interaction/re-find
            };
            cr.Add(lib.Element.RuntimeId);
            cr.Add(lib.Element.AutomationId);
            cr.Add(lib.Element.Name);
            cr.Add(lib.Element.ClassName);
            cr.Add(lib.Element.ControlType);
            cr.Add(lib.Element.ProcessId);
            cr.Add(lib.Element.IsOffscreen);
            cr.Add(lib.Element.BoundingRectangle);
            cr.Add(lib.PatternAvailability.IsInvokePatternAvailable);
            cr.Add(lib.PatternAvailability.IsTogglePatternAvailable);
            cr.Add(lib.PatternAvailability.IsExpandCollapsePatternAvailable);
            cr.Add(lib.PatternAvailability.IsSelectionItemPatternAvailable);
            cr.Add(lib.PatternAvailability.IsValuePatternAvailable);
            cr.Add(lib.Value.IsReadOnly);
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
            var lib = UiaRuntime.Properties;

            bool isOffscreen = Prop(element, lib.Element.IsOffscreen, false);

            Rectangle rect = Prop(element, lib.Element.BoundingRectangle, Rectangle.Empty);
            bool hasRect = !rect.IsEmpty;
            bool zeroSize = hasRect && rect.Width <= 0 && rect.Height <= 0;

            // Filter offscreen / zero-size nodes (and their subtrees) unless opted in.
            // The root window is never filtered, so a minimized app still returns a tree.
            if (!isRoot && !options.IncludeOffscreen && (isOffscreen || zeroSize))
                return null;

            string automationId = Prop(element, lib.Element.AutomationId, "") ?? "";
            string name = Prop(element, lib.Element.Name, "") ?? "";
            string className = Prop(element, lib.Element.ClassName, "") ?? "";
            var controlType = Prop(element, lib.Element.ControlType, ControlType.Unknown);
            string controlTypeName = AutomationMapper.ControlTypeName(controlType);
            int processId = Prop(element, lib.Element.ProcessId, 0);

            // An element is "clickable" if it exposes any pattern an agent can act on:
            // Invoke (buttons), Toggle (checkboxes), ExpandCollapse (tree/combo nodes),
            // SelectionItem (list/tab/radio items), a settable Value (editable fields), or it is a
            // Hyperlink control. The earlier invoke||toggle-only check missed most of these.
            bool invoke = Prop(element, lib.PatternAvailability.IsInvokePatternAvailable, false);
            bool toggle = Prop(element, lib.PatternAvailability.IsTogglePatternAvailable, false);
            bool expandCollapse = Prop(element, lib.PatternAvailability.IsExpandCollapsePatternAvailable, false);
            bool selectionItem = Prop(element, lib.PatternAvailability.IsSelectionItemPatternAvailable, false);
            bool valueSettable = Prop(element, lib.PatternAvailability.IsValuePatternAvailable, false)
                                 && !Prop(element, lib.Value.IsReadOnly, true);
            bool isHyperlink = controlType == ControlType.Hyperlink;
            bool clickable = invoke || toggle || expandCollapse || selectionItem || valueSettable || isHyperlink;

            var runtimeIdParts = Prop<int[]>(element, lib.Element.RuntimeId, null);
            string runtimeId = runtimeIdParts != null ? string.Join(",", runtimeIdParts) : "";
            if (string.IsNullOrEmpty(runtimeId))
            {
                try { runtimeId = UiaRuntime.RuntimeIdOf(element); }
                catch { /* element without a runtime id — leave empty, not addressable */ }
            }

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
                BoundingRectangle = hasRect ? $"{rect.Left},{rect.Top},{rect.Width},{rect.Height}" : null
            };

            var children = GetChildElements(element, ctx.Cached);

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
        /// Child elements via the cached snapshot (no COM round-trips) when this build runs under
        /// an active <see cref="CacheRequest"/>, falling back to a live ControlView TreeWalker.
        /// </summary>
        private static List<AutomationElement> GetChildElements(AutomationElement element, bool cached)
        {
            if (cached)
            {
                try
                {
                    return new List<AutomationElement>(element.CachedChildren);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[AppStructureHandler] CachedChildren unavailable, reading live: {ex.Message}");
                }
            }
            return ElementHandler.GetChildElements(element);
        }

        private string Serialize(AppNode root, AppStructureOptions? options = null) =>
            JsonConvert.SerializeObject(root, (options ?? _options).CompactJson ? Formatting.None : Formatting.Indented, _jsonSettings);

        /// <summary>
        /// Serializes the build result, honouring diff_mode: when a previous snapshot of the same
        /// root exists, only added/changed/removed nodes are returned. The fresh tree is always
        /// stored as the next diff base — including on non-diff calls, so a plain "See" followed
        /// by diff-mode actions works naturally.
        /// </summary>
        private string SerializeWithDiff(AppNode rootNode, int pid, bool diffMode, AppStructureOptions effective, out string note)
        {
            note = "";
            var previous = diffMode ? StructureSnapshotStore.Get(rootNode.UniqId) : null;
            StructureSnapshotStore.Set(rootNode.UniqId, pid, rootNode);

            if (!diffMode)
                return Serialize(rootNode, effective);

            if (previous == null)
            {
                note = " Diff requested but no previous snapshot existed; returned the full tree.";
                return Serialize(rootNode, effective);
            }

            var diff = StructureDiff.Compute(previous, rootNode);
            if (diff.IsEmpty)
                note = " No UI changes since the previous snapshot.";
            return JsonConvert.SerializeObject(diff, effective.CompactJson ? Formatting.None : Formatting.Indented, _jsonSettings);
        }

        private static string DescribeResult(BuildContext ctx) =>
            ctx.Truncated
                ? $"Structure retrieved ({ctx.Emitted} nodes, truncated — raise Features:AppStructure MaxDepth/MaxNodes to see more)."
                : $"Structure retrieved ({ctx.Emitted} nodes).";

        // --- property reader: cached when the build runs under an active CacheRequest, with a
        // --- per-property fallback default so one unsupported property never fails the node ---

        private static T Prop<T>(AutomationElement element, FlaUI.Core.Identifiers.PropertyId property, T fallback)
        {
            try
            {
                return element.FrameworkAutomationElement.TryGetPropertyValue<T>(property, out var value)
                    ? value
                    : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private sealed class BuildContext
        {
            public CancellationToken Ct;
            public AppStructureOptions Options = new();
            public int Emitted;
            public bool Truncated;
            public bool Cached;
        }

        /// <summary>
        /// True when the element's control type is <see cref="ControlType.Window"/> — i.e. a real
        /// top-level application window rather than a transient popup/menu/pane.
        /// </summary>
        private static bool IsWindowControlType(AutomationElement element)
        {
            try
            {
                return element.Properties.ControlType.ValueOrDefault == ControlType.Window;
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
            var walker = UiaRuntime.Automation.TreeWalkerFactory.GetControlViewWalker();
            return walker.GetFirstChild(element) != null;
        }
    }
}
