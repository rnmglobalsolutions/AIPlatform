using System.Diagnostics;
using AIMultiAgentPlatform.Infrastructure.Observability;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace AIMultiAgentPlatform.Workers.Observability;

public sealed class CorrelationFunctionMiddleware(ILoggerFactory loggerFactory) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var correlationId = await ResolveCorrelationIdAsync(context);
        context.Items[CorrelationConstants.ItemKey] = correlationId;
        Activity.Current?.SetTag("app.correlation_id", correlationId);

        var logger = loggerFactory.CreateLogger<CorrelationFunctionMiddleware>();
        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["FunctionName"] = context.FunctionDefinition.Name,
                   ["InvocationId"] = context.InvocationId
               }))
        {
            await next(context);
        }
    }

    private static async Task<string> ResolveCorrelationIdAsync(FunctionContext context)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request is not null &&
            request.Headers.TryGetValues(CorrelationConstants.HeaderName, out var values))
        {
            var headerValue = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(context.TraceContext.TraceParent))
        {
            return context.TraceContext.TraceParent;
        }

        return context.InvocationId;
    }
}
