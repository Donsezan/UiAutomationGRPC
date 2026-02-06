using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace UiAutomationGRPC.Server.Services
{
    public class AuthInterceptor : Interceptor
    {
        private readonly string _authToken;
        private const string AuthHeaderKey = "x-auth-token";

        public AuthInterceptor(string authToken)
        {
            _authToken = authToken;
        }

        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            if (!string.IsNullOrEmpty(_authToken))
            {
                var token = context.RequestHeaders.FirstOrDefault(h => h.Key == AuthHeaderKey)?.Value;
                if (token != _authToken)
                {
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or missing auth token."));
                }
            }
            return continuation(request, context);
        }

        // Note: If we use streaming, we would also need to override ServerStreamingServerHandler, etc.
        // But based on uiautomation.proto, it's all unary.
    }
}
