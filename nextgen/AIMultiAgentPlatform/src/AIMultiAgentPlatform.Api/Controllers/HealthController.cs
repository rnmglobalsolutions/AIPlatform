using AIMultiAgentPlatform.Infrastructure.Observability;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok(new
        {
            status = "Healthy",
            checkedUtc = DateTimeOffset.UtcNow
        });
    }

    [HttpGet("ready")]
    [ProducesResponseType(typeof(PlatformReadinessReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PlatformReadinessReport), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Ready([FromServices] PlatformOperationalReadinessService readinessService)
    {
        var report = readinessService.GetReport();
        return report.IsReady ? Ok(report) : StatusCode(StatusCodes.Status503ServiceUnavailable, report);
    }
}
