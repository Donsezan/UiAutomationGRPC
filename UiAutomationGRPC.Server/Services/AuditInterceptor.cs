using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Diagnostics;

namespace UiAutomationGRPC.Server.Services;

/// <summary>
/// gRPC interceptor for audit logging.
/// Logs all gRPC calls with timestamp, client IP, method, duration, and result.
/// </summary>
public class AuditInterceptor : Interceptor
{
    private readonly ILogger<AuditInterceptor> _logger;

    public AuditInterceptor(ILogger<AuditInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Method;
        var peer = context.Peer ?? "unknown";
        
        try
        {
            var response = await continuation(request, context);
            stopwatch.Stop();
            
            _logger.LogInformation(
                "gRPC Call: Method={Method} | Client={Client} | Duration={Duration}ms | Status=OK",
                method, peer, stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (RpcException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "gRPC Call: Method={Method} | Client={Client} | Duration={Duration}ms | Status={Status} | Message={Message}",
                method, peer, stopwatch.ElapsedMilliseconds, ex.StatusCode, ex.Status.Detail);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // Log the full exception server-side (Event Log / Trace) but do NOT echo the raw
            // exception text back to the client — it can leak paths, internals, and stack detail.
            // Business failures are already surfaced as { Success = false, Message } by the handlers;
            // reaching here means a genuinely unexpected fault, so return an opaque Internal status.
            _logger.LogError(ex,
                "gRPC Call: Method={Method} | Client={Client} | Duration={Duration}ms | Status=INTERNAL_ERROR",
                method, peer, stopwatch.ElapsedMilliseconds);
            throw new RpcException(new Status(StatusCode.Internal,
                "An internal server error occurred. See the server logs for details."));
        }
    }
}
