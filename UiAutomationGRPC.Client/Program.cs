using System.Text.Json;
using UiAutomation;                        // Generated proto types: ActionType, ReflectionTarget, FindElementRequest, ...
using UiAutomationGRPC.Client.Calc.Pages;  // Page Object Model sample (structured QA pattern)
using UiAutomationGRPC.Library;            // UiAutomationDriver — the entry point
using UiAutomationGRPC.Library.Helpers;    // VirtualMouse, VirtualKeyboard

namespace UiAutomationGRPC.Client;

/// <summary>
/// End-to-end reference for <b>UiAutomationGRPC</b>. It is split into two tours that together
/// exercise every capability of the server and the client Library:
///
///   PART 1 — Page Object Model (Calculator): the structured pattern a QA suite would use.
///            Fluent <see cref="UiAutomationGRPC.Library.Selectors.Selector"/> paths + element helpers.
///
///   PART 2 — "See → Think → Act" (Notepad): the loop an LLM agent uses.
///            GetAppStructure (See) → parse JSON / pick a RuntimeId (Think) →
///            PerformActionWithStructure / SendKeys / mouse (Act), plus the raw element RPCs,
///            the Reflection API, screenshots and cache management.
///
/// Calculator is a UWP/Store app, so <c>OpenApp</c> returns a launcher PID and <c>GetAppStructure</c>
/// can't always resolve its window — that is exactly why PART 1 drives it through the desktop-rooted
/// Selector API instead. Notepad is a classic Win32 app with a reliable PID, so PART 2 uses it for the
/// PID/structure-based features.
/// </summary>
internal static class Program
{
    private const string ServerAddress = "http://127.0.0.1:50051";

    private static async Task Main()
    {
        Console.WriteLine("UiAutomationGRPC — capability tour");
        Console.WriteLine($"Connecting to {ServerAddress} ...");

        // The UiAutomationDriver owns the gRPC channel and exposes one async method per RPC.
        // Three connection modes are supported (pick ONE):
        //
        //   Dev / loopback (no TLS):
        //     new UiAutomationDriver("http://127.0.0.1:50051", insecureMode: true)
        //   Production, OS-trusted certificate + bearer token:
        //     new UiAutomationDriver("https://host:50051", authToken: "<token>")
        //   Production, pinned self-signed certificate (no OS install needed):
        //     new UiAutomationDriver("https://host:50051", certificatePath: "server.pfx", authToken: "<token>")
        await using var driver = new UiAutomationDriver(ServerAddress, insecureMode: true);

        try
        {
            await Section("PART 1 — Page Object Model (Calculator)", () => RunPageObjectModelTour(driver));
            await Section("PART 2 — See → Think → Act (Notepad)", () => RunAgentLoopTour(driver));
            await Section("PART 3 — Access control (server BlackList)", () => RunAccessControlDemo(driver));

            // Teardown: drop every cached element on the server.
            var (cleared, clearMsg) = await driver.ClearCacheAsync();
            Console.WriteLine($"\nTeardown — ClearCache(all): success={cleared}, {clearMsg}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal: {ex.Message}");
        }

        Console.WriteLine("\nDone.");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PART 1 — Page Object Model: the structured pattern for QA automation.
    //   Selectors describe *how* to find an element (lazily); Page Objects describe
    //   *what* to do with a screen. See Calc/Pages/*.cs for the definitions.
    // ──────────────────────────────────────────────────────────────────────────────
    private static async Task RunPageObjectModelTour(UiAutomationDriver driver)
    {
        // 1. Lifecycle — open the app. (UWP: ProcessId is a launcher PID, so we don't rely on it here.)
        var (opened, openMsg, _) = await driver.OpenAppAsync("calc");
        if (!opened)
        {
            Console.WriteLine($"  Could not open Calculator: {openMsg}");
            return;
        }
        await Task.Delay(1500); // let the window settle

        // 2. Drive the page through its fluent API. Each Click* enqueues an action; the chain is
        //    flushed when a value-returning method (GetResult) is awaited. Computes 2 + 2.
        var calc = new CalcPage(driver);
        await calc.WaitForReady();
        calc.ClickTwo().ClickPlus().ClickTwo().ClickEqual();
        Console.WriteLine($"  2 + 2  →  {await calc.GetResult()}");

        // 3. Element-level helpers exposed by every IAutomationElement (resolved on demand via the
        //    Selector chain). These map to FindElement + GetProperty under the hood.
        var locators = new CalcPageLocators(driver);
        var two = locators.ButtonTwo;
        Console.WriteLine($"  '2' button RuntimeId : {await two.GetRuntimeIdAsync()}");
        Console.WriteLine($"  '2' button bounds    : {await two.GetRectangleAsync()}");
        Console.WriteLine($"  '2' button exists?   : {await two.IsElementExistAsync()}");

        // 4. Multi-page navigation — each transition returns the next strongly-typed Page Object.
        //    CalcPage → (hamburger) → CalcNavigationPage → (Settings) → CalcSettingsPage → (Back) → CalcPage
        var settings = await (await calc.OpenNavigation()).OpenSettings();
        Console.WriteLine($"  Settings page header : {await settings.GetTitle()}");
        Console.WriteLine($"  Calculator version   : {await settings.GetBuildVersion()}");
        calc = await settings.ClickBack();

        // 5. Mode switch through the same navigation pane, verified via the mode header.
        calc = await (await calc.OpenNavigation()).SwitchToScientific();
        Console.WriteLine($"  After switch         : {await calc.GetMode()}");
        calc = await (await calc.OpenNavigation()).SwitchToStandard();
        Console.WriteLine($"  Back to              : {await calc.GetMode()}");

        // 6. Close by name (resolves the app name → PID(s) like CloseApp does on the server).
        var (closed, closeMsg) = await driver.CloseAppAsync("CalculatorApp");
        Console.WriteLine($"  CloseApp(CalculatorApp): success={closed}, {closeMsg}");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PART 3 — Server-side access control. The server's appsettings.json blacklists
    //   Task Manager (top-level "BlackList"), so OpenApp on it is refused by the
    //   AppAccessValidator before any process is launched. Apps that aren't blacklisted
    //   (calc, notepad above) launch normally. This is gated entirely on the server — the
    //   client just sees a {Success=false, Message} result.
    // ──────────────────────────────────────────────────────────────────────────────
    private static async Task RunAccessControlDemo(UiAutomationDriver driver)
    {
        // The server resolves "taskmgr" via PATH and matches it against the blacklisted full path.
        var (opened, message, pid) = await driver.OpenAppAsync("taskmgr");
        if (opened)
        {
            Console.WriteLine($"  Task Manager LAUNCHED (PID {pid}) — blacklist is NOT active. Close it manually.");
            await driver.CloseAppByProcessIdAsync(pid);
        }
        else
        {
            Console.WriteLine($"  OpenApp(taskmgr) correctly refused by the server blacklist:");
            Console.WriteLine($"    → {message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // PART 2 — See → Think → Act: the loop an LLM agent runs, plus the raw RPC surface.
    // ──────────────────────────────────────────────────────────────────────────────
    private static async Task RunAgentLoopTour(UiAutomationDriver driver)
    {
        // ── Lifecycle ──────────────────────────────────────────────────────────────
        // Launch the classic Win32 Notepad by its full path. We deliberately avoid the bare name
        // "notepad": on current Windows that hits an App Execution Alias that redirects to the
        // packaged Store Notepad, which (like a UWP app) returns a launcher PID 0 that the
        // PID/structure calls can't use. The full System32 path runs the real Win32 binary, so
        // OpenApp returns a usable PID and the process is resolvable by name for GetAppStructure.
        string notepadPath = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        var (opened, openMsg, pid) = await driver.OpenAppAsync(notepadPath);
        if (!opened)
        {
            Console.WriteLine($"  Could not open Notepad: {openMsg}");
            return;
        }
        Console.WriteLine($"  Notepad opened (PID {pid}).");
        await Task.Delay(1200);

        try
        {
            // ── SEE ─────────────────────────────────────────────────────────────────
            // GetAppStructure returns the whole tree as compact JSON (offscreen nodes filtered,
            // depth/node-capped). GetAppStructure only *reads* — the app must already be running.
            var (seen, seeMsg, json) = await driver.GetAppStructureAsync("notepad");
            if (!seen)
            {
                Console.WriteLine($"  GetAppStructure failed: {seeMsg}");
                await driver.CloseAppByProcessIdAsync(pid);
                return;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Console.WriteLine($"  GetAppStructure: {seeMsg}");
            Console.WriteLine($"  Root window     : \"{Str(root, "Name")}\" [{Short(Str(root, "ControlType"))}]");
            Console.WriteLine($"  Nodes in tree   : {CountNodes(root)}");

            // ── THINK ─────────────────────────────────────────────────────────────────
            // Pick the editable text area out of the tree. Every node carries its RuntimeId in
            // "UniqId" — the handle used by all subsequent action / property / screenshot calls.
            var editNode = FindFirst(root, n =>
            {
                var ct = Str(n, "ControlType");
                return ct.Contains("Edit") || ct.Contains("Document");
            });
            if (editNode is null)
            {
                Console.WriteLine("  Could not locate the edit control in the tree.");
                await driver.CloseAppByProcessIdAsync(pid);
                return;
            }
            string editId = Str(editNode.Value, "UniqId");
            Console.WriteLine($"  Chosen element  : {Short(Str(editNode.Value, "ControlType"))}  (RuntimeId {editId})");

            // ── ACT 1: targeted keyboard ────────────────────────────────────────────────
            // SendToElement focuses the element first, then types — so keys land on *that* control
            // rather than whatever happened to have focus.
            var keyboard = new VirtualKeyboard(driver);
            await keyboard.SendToElementAsync(editId,
                "Hello from UiAutomationGRPC!{ENTER}This line was typed into the focused edit control.");
            Console.WriteLine("  SendToElement   : typed two lines into the edit control.");

            // ── ACT 2: action + refreshed tree in one round-trip ─────────────────────────
            // PerformActionWithStructure performs a UIA action and returns the freshly-rebuilt tree,
            // saving a separate GetAppStructure call. We focus the edit control; the refreshed tree's
            // root reflects the just-typed text (the window title is now "*Untitled - Notepad").
            // (Pick any ActionType here — Invoke/Toggle/SetValue/… — the server reports unsupported
            //  patterns honestly via Success/Message instead of throwing.)
            var (acted, actMsg, json2) = await driver.PerformActionWithStructureAsync(editId, ActionType.SetFocus);
            if (acted && !string.IsNullOrEmpty(json2))
            {
                using var doc2 = JsonDocument.Parse(json2);
                Console.WriteLine($"  PerformActionWithStructure: ok — refreshed tree ({CountNodes(doc2.RootElement)} nodes), " +
                                  $"window now \"{Str(doc2.RootElement, "Name")}\".");
            }
            else
            {
                Console.WriteLine($"  PerformActionWithStructure: {actMsg}");
            }

            // ── Raw element RPCs (what the Library is built on) ──────────────────────────
            // GetChildren — immediate children of the root window (by RuntimeId).
            var (gotKids, kidsMsg, children) = await driver.GetChildrenAsync(Str(root, "UniqId"));
            Console.WriteLine($"  GetChildren     : {(gotKids ? $"{children.Count} direct children" : kidsMsg)}");

            // GetProperty — read a single property off any cached element.
            var (gotProp, propVal, propMsg) = await driver.GetPropertyAsync(editId, "BoundingRectangle");
            Console.WriteLine($"  GetProperty     : Edit BoundingRectangle = {(gotProp ? propVal : propMsg)}");

            // FindElement — locate by a property condition, optionally starting from a known element.
            // (String properties like Name/AutomationId/ClassName map directly; ControlType is matched
            //  by the higher-level Selector API instead.)
            var find = await driver.FindElementAsync(new FindElementRequest
            {
                StartRuntimeId = Str(root, "UniqId"),
                Scope = UiAutomation.TreeScope.Descendants,
                Condition = new Condition
                {
                    PropertyCondition = new PropertyCondition
                    {
                        PropertyName = "ClassName",
                        PropertyValue = "Edit",
                        PropertyType = PropertyType.String
                    }
                }
            });
            Console.WriteLine($"  FindElement     : ClassName='Edit' → success={find.Success}, id={find.RuntimeId}");

            // ── Reflection API: introspect UIA metadata and per-element capabilities ─────
            var supported = await driver.ReflectAsync(ReflectionTarget.ElementSupportedPatterns, editId);
            Console.WriteLine($"  Reflect         : edit supports {supported.Entries.Count} patterns " +
                              $"[{string.Join(", ", supported.Entries.Take(6).Select(e => e.Name))}]");
            var controlTypes = await driver.ReflectAsync(ReflectionTarget.ControlTypes);
            Console.WriteLine($"  Reflect         : server knows {controlTypes.Entries.Count} control types.");

            // ── VirtualMouse: simulated, OS-level cursor input ───────────────────────────
            var mouse = new VirtualMouse(driver);
            await mouse.MoveToAsync(editId);   // move to the element's clickable point
            await mouse.LeftClickAsync();      // click where the cursor now is
            await mouse.ScrollStepsAsync(-2);  // scroll down two notches
            Console.WriteLine("  VirtualMouse    : moved to edit, clicked, scrolled.");

            // ── Screenshots (PNG bytes returned over the wire) ───────────────────────────
            await SaveShot("notepad_edit.png",   await driver.TakeElementScreenshotAsync(editId));
            await SaveShot("notepad_window.png",  await driver.TakeWindowScreenshotAsync(runtimeId: editId)); // window + highlight box
            await SaveShot("notepad_by_pid.png",  await driver.TakeWindowScreenshotAsync(processId: pid));
            await SaveShot("full_screen.png",     await driver.TakeWindowScreenshotAsync());                  // whole desktop

            // ── Cache management — drop just this process's cached elements ──────────────
            var (cacheCleared, cacheMsg) = await driver.ClearCacheAsync(processId: pid);
            Console.WriteLine($"  ClearCache(pid) : success={cacheCleared}, {cacheMsg}");
        }
        finally
        {
            // ── Lifecycle: close by PID (forcible; avoids the unsaved-changes dialog) ────
            var (closed, closeMsg) = await driver.CloseAppByProcessIdAsync(pid);
            Console.WriteLine($"  CloseAppByProcessId({pid}): success={closed}, {closeMsg}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Runs a named demo section, isolating failures so the tour keeps going.</summary>
    private static async Task Section(string title, Func<Task> body)
    {
        Console.WriteLine($"\n==== {title} ====");
        try
        {
            await body();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] {title} failed: {ex.Message}");
        }
    }

    /// <summary>Writes screenshot bytes to disk when the call succeeded.</summary>
    private static async Task SaveShot(string file, (bool Success, string Message, byte[] ImageData) shot)
    {
        if (shot.Success && shot.ImageData.Length > 0)
        {
            await File.WriteAllBytesAsync(file, shot.ImageData);
            Console.WriteLine($"  Screenshot      : saved {file} ({shot.ImageData.Length:N0} bytes)");
        }
        else
        {
            Console.WriteLine($"  Screenshot      : {file} skipped — {shot.Message}");
        }
    }

    // --- Minimal JSON tree helpers (System.Text.Json) for walking the GetAppStructure result ---

    /// <summary>Reads a string property off a node, returning "" when absent.</summary>
    private static string Str(JsonElement node, string property) =>
        node.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>Trims the "ControlType." prefix that UIA programmatic names carry.</summary>
    private static string Short(string controlType) =>
        controlType.StartsWith("ControlType.") ? controlType["ControlType.".Length..] : controlType;

    /// <summary>Counts every node in the tree (root + all descendants).</summary>
    private static int CountNodes(JsonElement node)
    {
        int count = 1;
        if (node.TryGetProperty("Children", out var kids) && kids.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in kids.EnumerateArray())
                count += CountNodes(child);
        }
        return count;
    }

    /// <summary>Depth-first search for the first node matching <paramref name="match"/>.</summary>
    private static JsonElement? FindFirst(JsonElement node, Func<JsonElement, bool> match)
    {
        if (match(node)) return node;
        if (node.TryGetProperty("Children", out var kids) && kids.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in kids.EnumerateArray())
            {
                var found = FindFirst(child, match);
                if (found is not null) return found;
            }
        }
        return null;
    }
}
