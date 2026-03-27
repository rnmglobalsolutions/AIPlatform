using AIMultiAgentPlatform.Application.Publishing;
using AIMultiAgentPlatform.Contracts.Publishing;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/publishing/profiles")]
public sealed class PublishingProfilesController : ControllerBase
{
    [HttpGet("{tenantId}")]
    public async Task<IActionResult> ListAsync(
        string tenantId,
        [FromServices] ListConnectedPublishingProfilesUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(tenantId, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(new { detail = result.ErrorMessage, errorCode = result.ErrorCode });
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertAsync(
        [FromBody] UpsertConnectedPublishingProfileRequest request,
        [FromServices] UpsertConnectedPublishingProfileUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(request, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(new { detail = result.ErrorMessage, errorCode = result.ErrorCode });
        }

        return Ok(result.Value);
    }
}
