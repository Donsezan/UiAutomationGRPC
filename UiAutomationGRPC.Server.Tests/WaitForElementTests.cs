using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Diagnostics;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Tests
{
    [TestFixture]
    public class WaitPolicyTests
    {
        [TestCase(0, WaitPolicy.DefaultTimeoutMs)]
        [TestCase(-5, WaitPolicy.DefaultTimeoutMs)]
        [TestCase(500, 500)]
        [TestCase(WaitPolicy.MaxTimeoutMs, WaitPolicy.MaxTimeoutMs)]
        [TestCase(WaitPolicy.MaxTimeoutMs + 1, WaitPolicy.MaxTimeoutMs)]
        public void Normalize_ClampsTimeout(int input, int expected)
        {
            var (timeout, _) = WaitPolicy.Normalize(input, 0);
            Assert.That(timeout, Is.EqualTo(expected));
        }

        [TestCase(0, WaitPolicy.DefaultPollIntervalMs)]
        [TestCase(-1, WaitPolicy.DefaultPollIntervalMs)]
        [TestCase(10, WaitPolicy.MinPollIntervalMs)]
        [TestCase(300, 300)]
        [TestCase(99_999, WaitPolicy.MaxPollIntervalMs)]
        public void Normalize_ClampsPollInterval(int input, int expected)
        {
            var (_, poll) = WaitPolicy.Normalize(0, input);
            Assert.That(poll, Is.EqualTo(expected));
        }
    }

    /// <summary>
    /// Service-level tests for the <c>WaitForElement</c> orchestration loop. These run real UIA
    /// probes against the desktop root with CHILDREN scope (cheap) and a condition that cannot
    /// match, exercising the timeout path without depending on any specific application.
    /// </summary>
    [TestFixture]
    public class WaitForElementTests
    {
        private UiaExecutor _executor = null!;
        private UiAutomationService _service = null!;
        private FakeServerCallContext _ctx = null!;

        [SetUp]
        public void SetUp()
        {
            _executor = new UiaExecutor();
            _service = new UiAutomationService(NullLoggerFactory.Instance, _executor);
            _ctx = new FakeServerCallContext();
        }

        [TearDown]
        public void TearDown() => _executor.Dispose();

        private static WaitForElementRequest ImpossibleRequest(int timeoutMs, int pollMs) => new()
        {
            Scope = TreeScope.Children,
            TimeoutMs = timeoutMs,
            PollIntervalMs = pollMs,
            Condition = new Condition
            {
                PropertyCondition = new PropertyCondition
                {
                    PropertyName = "AutomationId",
                    PropertyValue = "no-such-element-9c4e1f",
                    PropertyType = PropertyType.String
                }
            }
        };

        [Test]
        public async Task WaitForElement_TimesOut_WhenElementNeverAppears()
        {
            var resp = await _service.WaitForElement(ImpossibleRequest(timeoutMs: 400, pollMs: 100), _ctx);

            Assert.Multiple(() =>
            {
                Assert.That(resp.Success, Is.False);
                Assert.That(resp.RuntimeId, Is.Empty);
                Assert.That(resp.Message, Does.Contain("did not appear within 400 ms"));
            });
        }

        [Test]
        public async Task WaitForElement_RespectsTimeoutBudget()
        {
            var sw = Stopwatch.StartNew();
            await _service.WaitForElement(ImpossibleRequest(timeoutMs: 300, pollMs: 100), _ctx);
            sw.Stop();

            // Budget 300 ms + one probe's UIA latency; generous upper bound to avoid flakiness.
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5_000));
        }

        [Test]
        public async Task WaitForElement_ReturnsImmediately_WhenElementExists()
        {
            // The desktop root always has at least one child window; TrueCondition matches the first.
            var request = new WaitForElementRequest
            {
                Scope = TreeScope.Children,
                TimeoutMs = 5_000,
                Condition = new Condition { TrueCondition = true }
            };

            var resp = await _service.WaitForElement(request, _ctx);

            Assert.Multiple(() =>
            {
                Assert.That(resp.Success, Is.True);
                Assert.That(resp.RuntimeId, Is.Not.Empty);
            });
        }
    }
}
