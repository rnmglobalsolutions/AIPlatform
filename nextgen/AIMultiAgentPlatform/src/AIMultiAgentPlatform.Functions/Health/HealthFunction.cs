using System.Net;
using AIMultiAgentPlatform.Infrastructure.Observability;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIMultiAgentPlatform.Functions.Health;

public sealed class HealthFunction(PlatformOperationalReadinessService readinessService)
{
    [Function("FunctionsHealthReady")]
    public async Task<HttpResponseData> ReadyAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "api/internal/health/ready")] HttpRequestData request)
    {
        var report = readinessService.GetReport();
        var response = request.CreateResponse(report.IsReady ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        await response.WriteAsJsonAsync(report);
        return response;
    }

    [Function("FunctionsHealthLive")]
    public async Task<HttpResponseData> LiveAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "api/internal/health/live")] HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            status = "Healthy",
            checkedUtc = DateTimeOffset.UtcNow
        });
        return response;
    }
}
