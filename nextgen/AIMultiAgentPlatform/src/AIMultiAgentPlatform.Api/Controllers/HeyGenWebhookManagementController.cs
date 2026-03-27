using AIMultiAgentPlatform.Application.Video;
using AIMultiAgentPlatform.Contracts.Video;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/video/heygen/webhook")]
public sealed class HeyGenWebhookManagementController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromServices] GetHeyGenWebhookEndpointUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return NotFound(new
            {
                title = "HeyGen webhook endpoint was not found.",
                detail = result.ErrorMessage,
                errorCode = result.ErrorCode
            });
        }

        return Ok(result.Value);
    }

    [HttpPost("ensure")]
    public async Task<IActionResult> EnsureAsync(
        [FromBody] EnsureHeyGenWebhookEndpointRequest? request,
        [FromServices] EnsureHeyGenWebhookEndpointUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(request ?? new EnsureHeyGenWebhookEndpointRequest(), cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(new
            {
                title = "HeyGen webhook endpoint could not be ensured.",
                detail = result.ErrorMessage,
                errorCode = result.ErrorCode
            });
        }

        return Ok(result.Value);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAsync(
        [FromServices] DeleteHeyGenWebhookEndpointUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(new
            {
                title = "HeyGen webhook endpoint could not be deleted.",
                detail = result.ErrorMessage,
                errorCode = result.ErrorCode
            });
        }

        return Ok(result.Value);
    }
}
