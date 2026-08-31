using Serilog.Context;

namespace FintechBackend.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Reuse incoming X-Correlation-ID or generate a new GUID
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var extractedId) &&
                            !string.IsNullOrWhiteSpace(extractedId)
            ? extractedId.ToString()
            : Guid.NewGuid().ToString("N");

        // 2. Attach X-Correlation-ID to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers.Append(CorrelationIdHeader, correlationId);
            }
            return Task.CompletedTask;
        });

        // 3. Push CorrelationId into Serilog LogContext for tracing
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}