using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AIMultiAgentPlatform.Workers;

public sealed class HealthFunction(ILogger<HealthFunction> logger)
{
    [Function("WorkerHealth")]
    public HttpResponseData Get(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "api/internal/worker-health")] HttpRequestData request)
    {
        logger.LogInformation("Worker function app health endpoint invoked at {timeUtc}", DateTimeOffset.UtcNow);

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.WriteString("AIMultiAgentPlatform worker function app is running.");
        return response;
    }
}
