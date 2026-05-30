using Grpc.Core;

namespace UiAutomationGRPC.Server.Tests
{
    /// <summary>
    /// Minimal <see cref="ServerCallContext"/> subclass for use in tests that need to
    /// pass a context to a service method but do not exercise any context functionality.
    /// </summary>
    internal sealed class FakeServerCallContext : ServerCallContext
    {
        private readonly Metadata _requestHeaders;

        public FakeServerCallContext(Metadata? requestHeaders = null)
        {
            _requestHeaders = requestHeaders ?? new Metadata();
        }

        protected override string MethodCore => "/fake.Service/Method";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "ipv4:127.0.0.1:0";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => _requestHeaders;
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new Metadata();
        protected override Status StatusCore { get => Status.DefaultSuccess; set { } }
        protected override WriteOptions? WriteOptionsCore { get => null; set { } }
        protected override AuthContext AuthContextCore => null!;
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
