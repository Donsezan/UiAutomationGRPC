using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Diagnostics;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Tests
{
    /// <summary>
    /// End-to-end smoke test: drives a real Notepad through the full handler pipeline
    /// (UiaExecutor → handlers → live UIA + synthesized input). This is the regression net the
    /// unit tests cannot provide — it fails when See→Think→Act breaks against a real app.
    ///
    /// Gated by UIA_SMOKE=1 because it launches and types into a real window: skipped in normal
    /// `dotnet test` runs, enabled in the dedicated CI smoke job (GitHub windows runners have an
    /// interactive desktop, so UIA and SendInput work there).
    /// </summary>
    [TestFixture]
    [Category("Smoke")]
    public class SmokeTests
    {
        private UiaExecutor _executor = null!;
        private UiAutomationService _service = null!;
        private FakeServerCallContext _ctx = null!;
        private int _notepadPid;

        [SetUp]
        public void SetUp()
        {
            if (Environment.GetEnvironmentVariable("UIA_SMOKE") != "1")
                Assert.Ignore("Smoke test skipped — set UIA_SMOKE=1 to run (launches a real Notepad).");

            ElementCache.Clear();
            _executor = new UiaExecutor();
            _service = new UiAutomationService(NullLoggerFactory.Instance, _executor);
            _ctx = new FakeServerCallContext();
        }

        [TearDown]
        public void TearDown()
        {
            if (_notepadPid > 0)
            {
                try { Process.GetProcessById(_notepadPid).Kill(); }
                catch { /* already gone */ }
            }
            // Kill leftovers defensively so a failed run doesn't poison the next one (CI runner reuse).
            foreach (var p in Process.GetProcessesByName("notepad"))
            {
                try { p.Kill(); } catch { }
            }
            _executor?.Dispose();
            ElementCache.Clear();
        }

        [Test]
        public async Task Notepad_OpenTypeReadClose_RoundTrip()
        {
            // ---- Open ----
            var open = await _service.OpenApp(new AppRequest { AppName = "notepad" }, _ctx);
            Assert.That(open.Success, Is.True, $"OpenApp failed: {open.Message}");
            _notepadPid = open.ProcessId;

            // ---- Wait for the window (the WaitForElement primitive, not a sleep loop) ----
            var window = await _service.WaitForElement(new WaitForElementRequest
            {
                Scope = TreeScope.Children,
                TimeoutMs = 15_000,
                Condition = new Condition
                {
                    PropertyCondition = new PropertyCondition
                    {
                        PropertyName = "ClassName",
                        PropertyValue = "Notepad"
                    }
                }
            }, _ctx);
            Assert.That(window.Success, Is.True, $"Notepad window did not appear: {window.Message}");

            // ---- Find the text area (classic Edit on Server, RichEditD2DPT document on Win11) ----
            var edit = await FindFirst(window.RuntimeId,
                ("ClassName", "Edit"),
                ("ClassName", "RichEditD2DPT"),
                ("ControlType", "Document"),
                ("ControlType", "Edit"));
            Assert.That(edit, Is.Not.Null, "No text area found in Notepad");

            // ---- Type into it (targeted send_keys: foreground + focus + SendInput) ----
            var typed = await _service.SendKeys(new SendKeysRequest
            {
                Keys = "Hello CI 123",
                Wait = true,
                RuntimeId = edit!.RuntimeId
            }, _ctx);
            Assert.That(typed.Success, Is.True, $"SendKeys failed: {typed.Message}");

            // ---- Read it back (GetProperty Value via ValuePattern) ----
            // The target app pumps the injected input asynchronously — poll briefly instead of
            // asserting on the first read.
            GetPropertyResponse value = new();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                value = await _service.GetProperty(new GetPropertyRequest
                {
                    RuntimeId = edit.RuntimeId,
                    PropertyName = "Value"
                }, _ctx);
                if (value.Success && value.Value.Contains("Hello CI 123"))
                    break;
                await Task.Delay(200);
            }
            Assert.Multiple(() =>
            {
                Assert.That(value.Success, Is.True, $"GetProperty(Value) failed: {value.Message}");
                Assert.That(value.Value, Does.Contain("Hello CI 123"));
            });

            // ---- Structure read (the LLM "See" step) sees the same window ----
            var structure = await _service.GetAppStructure(new AppStructureRequest
            {
                UseProcessId = true,
                ProcessId = _notepadPid
            }, _ctx);
            Assert.Multiple(() =>
            {
                Assert.That(structure.Success, Is.True, $"GetAppStructure failed: {structure.Message}");
                Assert.That(structure.JsonStructure, Is.Not.Empty);
                Assert.That(structure.JsonStructure, Does.Contain("UniqId"));
            });

            // ---- Close ----
            var closed = await _service.CloseAppByProcessId(
                new CloseAppByProcessIdRequest { ProcessId = _notepadPid }, _ctx);
            Assert.That(closed.Success, Is.True, $"CloseAppByProcessId failed: {closed.Message}");
            _notepadPid = 0;
        }

        /// <summary>Tries several (property, value) locators under a start element, first hit wins.</summary>
        private async Task<ElementResponse?> FindFirst(string startRuntimeId, params (string Prop, string Value)[] locators)
        {
            foreach (var (prop, val) in locators)
            {
                var found = await _service.FindElement(new FindElementRequest
                {
                    StartRuntimeId = startRuntimeId,
                    Scope = TreeScope.Descendants,
                    Condition = new Condition
                    {
                        PropertyCondition = new PropertyCondition
                        {
                            PropertyName = prop,
                            PropertyValue = val
                        }
                    }
                }, _ctx);
                if (found.Success) return found;
            }
            return null;
        }
    }
}
