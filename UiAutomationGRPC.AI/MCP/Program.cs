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

            string address = config.ServerAddress ?? (config.Insecure ? "http://localhost:50051" : "https://localhost:50051");
            string? token = config.AuthToken;
            bool allowUnsecureTls = config.AllowUnsecureTls;

            if (config.Insecure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("WARNING: Insecure mode is enabled. Communication will not be encrypted.");
                Console.ResetColor();
            }
            else if (address.StartsWith("http://"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("CRITICAL ERROR: Secure connection requested but ServerAddress uses 'http'.");
                Console.ResetColor();
                throw new InvalidOperationException("Secure connection requested but ServerAddress uses 'http'. Use 'https' for secure connections or set 'Insecure' to true.");
            }

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
