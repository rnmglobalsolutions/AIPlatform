using AIMultiAgentPlatform.Application.Publishing;
using AIMultiAgentPlatform.Contracts.Publishing;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/publishing")]
public sealed class PublishingController : ControllerBase
{
    [HttpPost("publish")]
    public async Task<IActionResult> PublishScheduledAsync(
        [FromBody] PublishScheduledContentRequest request,
        [FromServices] PublishScheduledContentUseCase useCase,
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
