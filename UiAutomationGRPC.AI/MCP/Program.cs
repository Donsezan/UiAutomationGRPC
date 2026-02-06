using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using UiAutomation;

namespace UiAutomationGRPC.LLM
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var config = ClientConfig.Load();

            string address = config.ServerAddress ?? Environment.GetEnvironmentVariable("UIA_SERVER_ADDRESS") ?? "http://localhost:50051";
            string? token = config.AuthToken ?? Environment.GetEnvironmentVariable("UIA_AUTH_TOKEN");
            bool allowUnsecureTls = config.AllowUnsecureTls || Environment.GetEnvironmentVariable("UIA_ALLOW_UNSECURE_TLS")?.ToLower() == "true";

            try 
            {
                var channelOptions = new GrpcChannelOptions();
                if (allowUnsecureTls || address.StartsWith("https"))
                {
                    var httpHandler = new HttpClientHandler();
                    if (allowUnsecureTls)
                    {
                        httpHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    }
                    channelOptions.HttpHandler = httpHandler;
                }

                using var channel = GrpcChannel.ForAddress(address, channelOptions);

                UiAutomationService.UiAutomationServiceClient client;
                if (!string.IsNullOrEmpty(token))
                {
                    var invoker = channel.Intercept(new ClientAuthInterceptor(token));
                    client = new UiAutomationService.UiAutomationServiceClient(invoker);
                }
                else
                {
                    client = new UiAutomationService.UiAutomationServiceClient(channel);
                }
                
                var server = new McpServer(client);
                await server.RunAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private class ClientAuthInterceptor : Interceptor
        {
            private readonly string _token;
            public ClientAuthInterceptor(string token) => _token = token;

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
                TRequest request,
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                var metadata = context.Options.Headers ?? new Metadata();
                metadata.Add("x-auth-token", _token);

                var newOptions = context.Options.WithHeaders(metadata);
                var newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, newOptions);

                return continuation(request, newContext);
            }
        }
    }
}
