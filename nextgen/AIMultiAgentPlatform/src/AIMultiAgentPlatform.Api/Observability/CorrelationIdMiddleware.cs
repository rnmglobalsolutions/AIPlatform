using System.Diagnostics;
using AIMultiAgentPlatform.Infrastructure.Observability;

namespace AIMultiAgentPlatform.Api.Observability;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[CorrelationConstants.ItemKey] = correlationId;
        context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;
        Activity.Current?.SetTag("app.correlation_id", correlationId);

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["RequestPath"] = context.Request.Path.ToString()
               }))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationConstants.HeaderName, out var existingHeader) &&
            !string.IsNullOrWhiteSpace(existingHeader))
        {
            return existingHeader.ToString().Trim();
        }

        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            return traceId;
        }

        return context.TraceIdentifier;
    }
}
