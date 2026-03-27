using AIMultiAgentPlatform.Application.Publishing;
using AIMultiAgentPlatform.Contracts.Orchestration;
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

    [HttpPost("publish/enqueue")]
    [ProducesResponseType(typeof(CommandEnqueueResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnqueuePublishScheduledAsync(
        [FromBody] PublishScheduledContentRequest request,
        [FromServices] EnqueuePublishScheduledContentUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new PublishScheduledContentCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(new { detail = result.ErrorMessage, errorCode = result.ErrorCode });
        }

        return Accepted(result.Value);
    }

    [HttpPost("reconcile")]
    [ProducesResponseType(typeof(ReconcilePublishedContentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReconcileAsync(
        [FromBody] ReconcilePublishedContentRequest request,
        [FromServices] ReconcilePublishedContentUseCase useCase,
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
