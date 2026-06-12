using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using UiAutomation;

namespace UiAutomationGRPC.LLM;

/// <summary>
/// MCP tools that bridge an LLM client to the UiAutomationGRPC gRPC server.
/// Each method receives the gRPC client and a <see cref="CancellationToken"/> via DI — both are
/// resolved by the SDK and omitted from the generated JSON schema. The remaining parameters are the
/// tool inputs the model fills in.
///
/// Business outcomes are surfaced as a <see cref="CallToolResult"/> with <c>IsError</c> mirroring the
/// server's <c>Success</c> flag, so the model sees failures as readable tool results (not protocol faults).
/// </summary>
[McpServerToolType]
public static class UiAutomationTools
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private static CallToolResult Text(string text, bool isError) => new()
    {
        Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        IsError = isError
    };

    private static CallToolResult Json(object payload, bool isError) =>
        Text(JsonSerializer.Serialize(payload, JsonOpts), isError);

    // ---------------------------------------------------------------- App lifecycle

    [McpServerTool(Name = "open_app"), Description("Launches an application by name or executable path. Returns the launched process id. Note: for UWP/Store apps (e.g. 'calc') the returned PID may be a launcher/host process — prefer addressing such apps by name in get_app_structure.")]
    public static async Task<CallToolResult> OpenApp(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("Executable path or app name (e.g. 'notepad', 'calc').")] string appName,
        [Description("Optional command-line arguments.")] string? arguments = null,
        CancellationToken ct = default)
    {
        var resp = await client.OpenAppAsync(
            new AppRequest { AppName = appName, Arguments = arguments ?? "" }, cancellationToken: ct);
        return Json(new { success = resp.Success, message = resp.Message, process_id = resp.ProcessId }, !resp.Success);
    }

    [McpServerTool(Name = "close_app"), Description("Closes a running application by its process id.")]
    public static async Task<CallToolResult> CloseApp(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("Process id of the application to close.")] int processId,
        CancellationToken ct = default)
    {
        var resp = await client.CloseAppByProcessIdAsync(
            new CloseAppByProcessIdRequest { ProcessId = processId }, cancellationToken: ct);
        return Json(new { success = resp.Success, message = resp.Message }, !resp.Success);
    }

    // ---------------------------------------------------------------- See

    [McpServerTool(Name = "get_app_structure"), Description("Returns the UI tree of an application as a compact JSON string. Address the app either by process id (set useProcessId=true) or by name. Use this as the 'See' step of a See -> Think -> Act loop.")]
    public static async Task<CallToolResult> GetAppStructure(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("Set true to look the app up by process_id; false to look it up by app_name.")] bool useProcessId,
        [Description("Process id of the target app (used when use_process_id is true).")] int processId = 0,
        [Description("Name of the target app (used when use_process_id is false).")] string? appName = null,
        CancellationToken ct = default)
    {
        var resp = await client.GetAppStructureAsync(new AppStructureRequest
        {
            UseProcessId = useProcessId,
            ProcessId = processId,
            AppName = appName ?? ""
        }, cancellationToken: ct);
        return Text(resp.Success ? resp.JsonStructure : resp.Message, !resp.Success);
    }

    [McpServerTool(Name = "find_element"), Description("Finds a single element by a property condition (e.g. Name, AutomationId, ControlType, ClassName). Returns the element's runtime_id for use in other tools. Optionally scope the search under a known element via start_runtime_id.")]
    public static async Task<CallToolResult> FindElement(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("Property to match on, e.g. 'Name', 'AutomationId', 'ControlType', 'ClassName'.")] string propertyName,
        [Description("Value the property must equal.")] string propertyValue,
        [Description("Optional runtime_id to search under; empty searches from the desktop root.")] string? startRuntimeId = null,
        [Description("Search scope: ELEMENT, CHILDREN, DESCENDANTS (default), SUBTREE, PARENT, ANCESTORS.")] string? scope = null,
        [Description("Value type hint: STRING (default), BOOL, or INT.")] string? propertyType = null,
        CancellationToken ct = default)
    {
        var treeScope = ParseEnum(scope, TreeScope.Descendants);
        var propType = ParseEnum(propertyType, PropertyType.String);

        var resp = await client.FindElementAsync(new FindElementRequest
        {
            StartRuntimeId = startRuntimeId ?? "",
            Scope = treeScope,
            Condition = new Condition
            {
                PropertyCondition = new PropertyCondition
                {
                    PropertyName = propertyName,
                    PropertyValue = propertyValue,
                    PropertyType = propType
                }
            }
        }, cancellationToken: ct);

        return Json(new
        {
            success = resp.Success,
            message = resp.Message,
            runtime_id = resp.RuntimeId,
            name = resp.Name,
            automation_id = resp.AutomationId,
            class_name = resp.ClassName,
            control_type = resp.ControlType
        }, !resp.Success);
    }

    [McpServerTool(Name = "wait_for_element"), Description("Waits until an element matching a property condition appears, or the timeout elapses. Use this right after open_app (or after an action that opens a window/dialog) instead of retrying find_element in a loop — one call, the server polls the live UI tree. Returns the element's runtime_id on success.")]
    public static async Task<CallToolResult> WaitForElement(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("Property to match on, e.g. 'Name', 'AutomationId', 'ControlType', 'ClassName'.")] string propertyName,
        [Description("Value the property must equal.")] string propertyValue,
        [Description("Total wait budget in milliseconds (default 10000, max 120000).")] int timeoutMs = 0,
        [Description("Optional runtime_id to search under; empty searches from the desktop root.")] string? startRuntimeId = null,
        [Description("Search scope: ELEMENT, CHILDREN, DESCENDANTS (default), SUBTREE, PARENT, ANCESTORS.")] string? scope = null,
        CancellationToken ct = default)
    {
        var resp = await client.WaitForElementAsync(new WaitForElementRequest
        {
            StartRuntimeId = startRuntimeId ?? "",
            Scope = ParseEnum(scope, TreeScope.Descendants),
            TimeoutMs = timeoutMs,
            Condition = new Condition
            {
                PropertyCondition = new PropertyCondition
                {
                    PropertyName = propertyName,
                    PropertyValue = propertyValue,
                    PropertyType = PropertyType.String
                }
            }
        }, cancellationToken: ct);

        return Json(new
        {
            success = resp.Success,
            message = resp.Message,
            runtime_id = resp.RuntimeId,
            name = resp.Name,
            automation_id = resp.AutomationId,
            class_name = resp.ClassName,
            control_type = resp.ControlType
        }, !resp.Success);
    }

    [McpServerTool(Name = "get_children"), Description("Returns the immediate child elements of an element (or of the desktop when runtime_id is empty), each with its runtime_id and identifying properties.")]
    public static async Task<CallToolResult> GetChildren(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("runtime_id of the parent element; empty for the desktop root.")] string? runtimeId = null,
        CancellationToken ct = default)
    {
        var resp = await client.GetChildrenAsync(
            new GetChildrenRequest { RuntimeId = runtimeId ?? "" }, cancellationToken: ct);

        var children = resp.Elements.Select(e => new
        {
            runtime_id = e.RuntimeId,
            name = e.Name,
            automation_id = e.AutomationId,
            class_name = e.ClassName,
            control_type = e.ControlType
        });
        return Json(new { success = resp.Success, message = resp.Message, children }, !resp.Success);
    }

    [McpServerTool(Name = "get_property"), Description("Reads a single UI Automation property (e.g. 'Name', 'IsEnabled', 'Value') of an element by runtime_id.")]
    public static async Task<CallToolResult> GetProperty(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("runtime_id of the target element (from get_app_structure / find_element).")] string runtimeId,
        [Description("Property name to read, e.g. 'Name', 'IsEnabled', 'Value'.")] string propertyName,
        CancellationToken ct = default)
    {
        var resp = await client.GetPropertyAsync(
            new GetPropertyRequest { RuntimeId = runtimeId, PropertyName = propertyName }, cancellationToken: ct);
        return Json(new { success = resp.Success, message = resp.Message, value = resp.Value }, !resp.Success);
    }

    // ---------------------------------------------------------------- Act

    [McpServerTool(Name = "perform_action"), Description("Performs an action on a UI element by runtime_id. action is a value of the ActionType enum, e.g. INVOKE, TOGGLE, SELECT, EXPAND_COLLAPSE, SET_VALUE, SET_FOCUS, LeftClick, RightClick, DoubleClick. For SET_VALUE pass the text in arguments.")]
    public static async Task<CallToolResult> PerformAction(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("runtime_id of the target element.")] string runtimeId,
        [Description("Action name (e.g. INVOKE, TOGGLE, SET_VALUE, LeftClick).")] string action,
        [Description("Optional arguments, e.g. the text for SET_VALUE or the delta for a scroll.")] string[]? arguments = null,
        CancellationToken ct = default)
    {
        var resp = await client.PerformActionAsync(BuildActionRequest(runtimeId, action, arguments), cancellationToken: ct);
        return Json(new { success = resp.Success, message = resp.Message }, !resp.Success);
    }

    [McpServerTool(Name = "perform_action_with_structure"), Description("Performs an action on an element and returns the refreshed UI tree as compact JSON in one call. Ideal for the LLM 'See -> Think -> Act' loop. action / arguments behave like perform_action.")]
    public static async Task<CallToolResult> PerformActionWithStructure(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("runtime_id of the target element.")] string runtimeId,
        [Description("Action name (e.g. INVOKE, TOGGLE, SET_VALUE, LeftClick).")] string action,
        [Description("Optional arguments, e.g. the text for SET_VALUE.")] string[]? arguments = null,
        CancellationToken ct = default)
    {
        var resp = await client.PerformActionWithStructureAsync(
            BuildActionRequest(runtimeId, action, arguments), cancellationToken: ct);
        return Text(resp.Success ? resp.JsonStructure : resp.Message, !resp.Success);
    }

    [McpServerTool(Name = "send_keys"), Description("Sends keystrokes. When runtime_id is provided the element is focused first so keys land on that control; otherwise keys go to whatever currently has focus. Uses System.Windows.Forms.SendKeys syntax (e.g. '{ENTER}', '^a' for Ctrl+A).")]
    public static async Task<CallToolResult> SendKeys(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("The keys to send (SendKeys syntax).")] string keys,
        [Description("runtime_id to focus before sending; empty sends to the current focus.")] string? runtimeId = null,
        [Description("Whether to wait for the keys to be processed (default true).")] bool wait = true,
        CancellationToken ct = default)
    {
        var resp = await client.SendKeysAsync(new SendKeysRequest
        {
            Keys = keys,
            Wait = wait,
            RuntimeId = runtimeId ?? ""
        }, cancellationToken: ct);
        return Json(new { success = resp.Success, message = resp.Message }, !resp.Success);
    }

    // ---------------------------------------------------------------- Screenshot (image content)

    [McpServerTool(Name = "take_screenshot"), Description("Captures a screenshot and returns it as image content the model can see. mode='element' captures a single element (runtime_id required); mode='window' captures the element's window, or a process's main window when only process_id is given.")]
    public static async Task<CallToolResult> TakeScreenshot(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("'element' or 'window'.")] string mode,
        [Description("runtime_id of the element (required for 'element' mode; optional for 'window').")] string? runtimeId = null,
        [Description("Process id (used for 'window' mode when runtime_id is not given).")] int processId = 0,
        CancellationToken ct = default)
    {
        var screenshotMode = string.Equals(mode, "element", StringComparison.OrdinalIgnoreCase)
            ? ScreenshotMode.Element
            : ScreenshotMode.Window;

        var resp = await client.TakeScreenshotAsync(new ScreenshotRequest
        {
            Mode = screenshotMode,
            RuntimeId = runtimeId ?? "",
            ProcessId = processId
        }, cancellationToken: ct);

        if (resp.Success && resp.ImageData is { Length: > 0 })
        {
            return new CallToolResult
            {
                Content = new List<ContentBlock>
                {
                    ImageContentBlock.FromBytes(resp.ImageData.Memory, "image/png")
                },
                IsError = false
            };
        }

        return Text(resp.Message.Length > 0 ? resp.Message : "Failed to take screenshot.", isError: true);
    }

    // ---------------------------------------------------------------- Cache

    [McpServerTool(Name = "clear_cache"), Description("Clears the server-side element cache. With no arguments clears everything; pass process_id or app_name to scope it to one application.")]
    public static async Task<CallToolResult> ClearCache(
        UiAutomationService.UiAutomationServiceClient client,
        [Description("Optional: clear cache for this process id only.")] int processId = 0,
        [Description("Optional: clear cache by application name (like close_app).")] string? appName = null,
        CancellationToken ct = default)
    {
        var req = new ClearCacheRequest();
        if (!string.IsNullOrEmpty(appName))
            req.AppName = appName;
        else if (processId > 0)
            req.ProcessId = processId;

        var resp = await client.ClearCacheAsync(req, cancellationToken: ct);
        return Json(new { success = resp.Success, message = resp.Message }, !resp.Success);
    }

    // ---------------------------------------------------------------- Helpers

    private static PerformActionRequest BuildActionRequest(string runtimeId, string action, string[]? arguments)
    {
        if (!Enum.TryParse<ActionType>(action, ignoreCase: true, out var actionType))
        {
            if (int.TryParse(action, out var actionInt))
                actionType = (ActionType)actionInt;
            else
                throw new ArgumentException($"Invalid action type: '{action}'.");
        }

        var req = new PerformActionRequest { RuntimeId = runtimeId, Action = actionType };
        if (arguments != null)
            req.Arguments.AddRange(arguments);
        return req;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
}
