using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using UiAutomation;
using UiAutomationGRPC.Server.Helpers;

namespace UiAutomationGRPC.Server.Tests
{
    [TestFixture]
    public class ServerStatusTests
    {
        private UiaExecutor _executor = null!;
        private UiAutomationService _service = null!;
        private FakeServerCallContext _ctx = null!;

        [SetUp]
        public void SetUp()
        {
            ElementCache.Clear();
            _executor = new UiaExecutor(maxQueueDepth: 7);
            _service = new UiAutomationService(NullLoggerFactory.Instance, _executor);
            _ctx = new FakeServerCallContext();
        }

        [TearDown]
        public void TearDown()
        {
            ElementCache.Clear();
            _executor.Dispose();
        }

        [Test]
        public async Task GetServerStatus_ReportsQueueAndCacheState()
        {
            ElementCache.CacheElement(null!, "42,1", "id", "name", "cls", "ControlType.Button", 999);

            var resp = await _service.GetServerStatus(new ServerStatusRequest(), _ctx);

            Assert.Multiple(() =>
            {
                Assert.That(resp.Success, Is.True);
                Assert.That(resp.QueueCapacity, Is.EqualTo(7));
                Assert.That(resp.PendingRequests, Is.GreaterThanOrEqualTo(0));
                Assert.That(resp.CachedElements, Is.EqualTo(1));
                Assert.That(resp.ServerVersion, Is.Not.Empty);
            });
        }

        [Test]
        public async Task GetServerStatus_InteractiveTestRun_ReportsInteractiveSession()
        {
            // The test host runs in the user's interactive session, never Session 0.
            var resp = await _service.GetServerStatus(new ServerStatusRequest(), _ctx);

            Assert.Multiple(() =>
            {
                Assert.That(resp.SessionId, Is.Not.EqualTo(0));
                Assert.That(resp.InteractiveSession, Is.True);
                Assert.That(resp.Message, Is.EqualTo("OK"));
            });
        }

        [Test]
        public async Task GetServerStatus_ReflectsCacheEnabledFlag()
        {
            bool original = ElementCache.Enabled;
            try
            {
                ElementCache.Enabled = false;
                var resp = await _service.GetServerStatus(new ServerStatusRequest(), _ctx);
                Assert.That(resp.CacheEnabled, Is.False);
            }
            finally
            {
                ElementCache.Enabled = original;
            }
        }
    }
}
