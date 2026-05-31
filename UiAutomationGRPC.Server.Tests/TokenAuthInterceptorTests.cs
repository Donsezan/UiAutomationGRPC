using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using UiAutomationGRPC.Server.Services;

namespace UiAutomationGRPC.Server.Tests
{
    /// <summary>
    /// Tests for <see cref="TokenAuthInterceptor"/> token validation logic.
    /// No UIA session required — all tests exercise pure configuration/header logic.
    ///
    /// Critical regression guard: an empty <c>Security:ValidTokens</c> list with
    /// <c>TokenAuthEnabled=true</c> must reject every request (fail-closed).
    /// There is no startup warning for this — it is a documented gotcha in CLAUDE.md.
    /// </summary>
    [TestFixture]
    public class TokenAuthInterceptorTests
    {
        // ────────────────────────────── TokenAuthEnabled = false ──────────────────────────────

        [Test]
        public async Task TokenAuthDisabled_NoBearerHeader_PassesThroughToHandler()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: false);
            var context = new FakeServerCallContext();
            bool continuationCalled = false;

            await interceptor.UnaryServerHandler<object, object>(
                new object(), context,
                (_, _) => { continuationCalled = true; return Task.FromResult<object>("ok"); });

            Assert.That(continuationCalled, Is.True);
        }

        [Test]
        public async Task TokenAuthDisabled_WithValidTokens_BypassesCheckEntirely()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: false, validTokens: ["secret"]);
            var context = new FakeServerCallContext(); // no auth header
            bool continuationCalled = false;

            await interceptor.UnaryServerHandler<object, object>(
                new object(), context,
                (_, _) => { continuationCalled = true; return Task.FromResult<object>("ok"); });

            Assert.That(continuationCalled, Is.True);
        }

        // ────────────────────────────── Missing / empty header ──────────────────────────────

        [Test]
        public void TokenAuthEnabled_NoAuthorizationHeader_ThrowsUnauthenticated()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: ["secret"]);
            var context = new FakeServerCallContext(); // no headers

            var ex = Assert.ThrowsAsync<RpcException>(() =>
                interceptor.UnaryServerHandler<object, object>(
                    new object(), context, (_, _) => Task.FromResult<object>("ok")));

            Assert.That(ex!.StatusCode, Is.EqualTo(StatusCode.Unauthenticated));
        }

        // ────────────────────────────── Invalid token ──────────────────────────────

        [Test]
        public void TokenAuthEnabled_WrongBearerToken_ThrowsUnauthenticated()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: ["correct-token"]);
            var context = new FakeServerCallContext(
                new Metadata { { "authorization", "Bearer wrong-token" } });

            var ex = Assert.ThrowsAsync<RpcException>(() =>
                interceptor.UnaryServerHandler<object, object>(
                    new object(), context, (_, _) => Task.FromResult<object>("ok")));

            Assert.That(ex!.StatusCode, Is.EqualTo(StatusCode.Unauthenticated));
        }

        [Test]
        public void TokenAuthEnabled_PlainWrongToken_ThrowsUnauthenticated()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: ["correct-token"]);
            var context = new FakeServerCallContext(
                new Metadata { { "authorization", "wrong-token" } });

            var ex = Assert.ThrowsAsync<RpcException>(() =>
                interceptor.UnaryServerHandler<object, object>(
                    new object(), context, (_, _) => Task.FromResult<object>("ok")));

            Assert.That(ex!.StatusCode, Is.EqualTo(StatusCode.Unauthenticated));
        }

        // ────────────────────────────── Valid token ──────────────────────────────

        [Test]
        public async Task TokenAuthEnabled_ValidBearerToken_PassesThroughToHandler()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: ["my-token"]);
            var context = new FakeServerCallContext(
                new Metadata { { "authorization", "Bearer my-token" } });
            bool continuationCalled = false;

            await interceptor.UnaryServerHandler<object, object>(
                new object(), context,
                (_, _) => { continuationCalled = true; return Task.FromResult<object>("ok"); });

            Assert.That(continuationCalled, Is.True);
        }

        [Test]
        public async Task TokenAuthEnabled_ValidPlainToken_PassesThroughToHandler()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: ["my-token"]);
            var context = new FakeServerCallContext(
                new Metadata { { "authorization", "my-token" } });
            bool continuationCalled = false;

            await interceptor.UnaryServerHandler<object, object>(
                new object(), context,
                (_, _) => { continuationCalled = true; return Task.FromResult<object>("ok"); });

            Assert.That(continuationCalled, Is.True);
        }

        [Test]
        public async Task TokenAuthEnabled_BearerPrefixCaseInsensitive_PassesThrough()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: ["my-token"]);
            var context = new FakeServerCallContext(
                new Metadata { { "authorization", "BEARER my-token" } });
            bool continuationCalled = false;

            await interceptor.UnaryServerHandler<object, object>(
                new object(), context,
                (_, _) => { continuationCalled = true; return Task.FromResult<object>("ok"); });

            Assert.That(continuationCalled, Is.True);
        }

        [Test]
        public async Task TokenAuthEnabled_OneOfMultipleValidTokens_PassesThrough()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: ["token-a", "token-b", "token-c"]);
            var context = new FakeServerCallContext(
                new Metadata { { "authorization", "Bearer token-b" } });
            bool continuationCalled = false;

            await interceptor.UnaryServerHandler<object, object>(
                new object(), context,
                (_, _) => { continuationCalled = true; return Task.FromResult<object>("ok"); });

            Assert.That(continuationCalled, Is.True);
        }

        // ────────────────────────────── Fail-closed: empty ValidTokens ──────────────────────────────
        // DOCUMENTED GOTCHA (CLAUDE.md): enabling token auth with an empty ValidTokens list
        // silently rejects EVERY request — there is no startup warning.

        [Test]
        public void EmptyValidTokensList_RejectsEveryRequest_FailClosed()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: []);
            var context = new FakeServerCallContext(
                new Metadata { { "authorization", "Bearer any-token" } });

            var ex = Assert.ThrowsAsync<RpcException>(() =>
                interceptor.UnaryServerHandler<object, object>(
                    new object(), context, (_, _) => Task.FromResult<object>("ok")));

            Assert.That(ex!.StatusCode, Is.EqualTo(StatusCode.Unauthenticated),
                "Empty ValidTokens must be fail-closed — every token is invalid when the list is empty.");
        }

        [Test]
        public void EmptyValidTokensList_NoHeader_AlsoRejected()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: []);
            var context = new FakeServerCallContext(); // no header

            var ex = Assert.ThrowsAsync<RpcException>(() =>
                interceptor.UnaryServerHandler<object, object>(
                    new object(), context, (_, _) => Task.FromResult<object>("ok")));

            Assert.That(ex!.StatusCode, Is.EqualTo(StatusCode.Unauthenticated));
        }

        // ────────────────────────────── Token comparison is case-sensitive ──────────────────────────────

        [Test]
        public void TokenComparison_IsCaseSensitive()
        {
            var interceptor = CreateInterceptor(tokenAuthEnabled: true, validTokens: ["Secret"]);
            var context = new FakeServerCallContext(
                new Metadata { { "authorization", "Bearer secret" } }); // lowercase

            var ex = Assert.ThrowsAsync<RpcException>(() =>
                interceptor.UnaryServerHandler<object, object>(
                    new object(), context, (_, _) => Task.FromResult<object>("ok")));

            Assert.That(ex!.StatusCode, Is.EqualTo(StatusCode.Unauthenticated));
        }

        // ────────────────────────────── Helper ──────────────────────────────

        private static TokenAuthInterceptor CreateInterceptor(
            bool tokenAuthEnabled = false,
            string[]? validTokens = null)
        {
            var tokens = validTokens ?? [];
            var data = new Dictionary<string, string?>
            {
                ["Security:TokenAuthEnabled"] = tokenAuthEnabled.ToString().ToLower()
            };
            for (int i = 0; i < tokens.Length; i++)
                data[$"Security:ValidTokens:{i}"] = tokens[i];

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(data)
                .Build();

            return new TokenAuthInterceptor(config, NullLogger<TokenAuthInterceptor>.Instance);
        }
    }
}
