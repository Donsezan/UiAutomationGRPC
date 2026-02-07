using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace UiAutomationGRPC.Server.Services;

/// <summary>
/// gRPC interceptor for token-based authentication.
/// Validates Bearer tokens from Authorization header against configured valid tokens.
/// </summary>
public class TokenAuthInterceptor : Interceptor
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenAuthInterceptor> _logger;
    private readonly string[] _validTokens;

    public TokenAuthInterceptor(IConfiguration configuration, ILogger<TokenAuthInterceptor> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _validTokens = _configuration.GetSection("Security:ValidTokens").Get<string[]>() ?? Array.Empty<string>();
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        // Check if token auth is enabled
        if (!_configuration.GetValue<bool>("Security:TokenAuthEnabled"))
        {
            return await continuation(request, context);
        }

        var token = GetTokenFromMetadata(context);
        
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Authentication failed: No token provided. Client: {Peer}, Method: {Method}", 
                context.Peer, context.Method);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authorization token is required"));
        }

        if (!IsValidToken(token))
        {
            _logger.LogWarning("Authentication failed: Invalid token. Client: {Peer}, Method: {Method}", 
                context.Peer, context.Method);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid authorization token"));
        }

        _logger.LogDebug("Authentication succeeded for client: {Peer}", context.Peer);
        return await continuation(request, context);
    }

    private string? GetTokenFromMetadata(ServerCallContext context)
    {
        var authHeader = context.RequestHeaders.FirstOrDefault(h => 
            h.Key.Equals("authorization", StringComparison.OrdinalIgnoreCase));
        
        if (authHeader == null)
            return null;

        var value = authHeader.Value;
        
        // Support both "Bearer <token>" and plain "<token>" formats
        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring(7).Trim();
        }
        
        return value.Trim();
    }

    private bool IsValidToken(string token)
    {
        return _validTokens.Contains(token, StringComparer.Ordinal);
    }
}
