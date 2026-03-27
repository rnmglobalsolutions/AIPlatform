using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Contracts.Orchestration;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/intake/tally")]
public sealed class TallyIntakeController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TallySubmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostAsync(
        [FromBody] TallySubmissionRequest request,
        [FromServices] ProcessTallySubmissionUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Tally submission could not be processed.",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(PostAsync), result.Value);
    }

    [HttpPost("enqueue")]
    [ProducesResponseType(typeof(CommandEnqueueResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnqueueAsync(
        [FromBody] TallySubmissionRequest request,
        [FromServices] EnqueueProcessTallySubmissionUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(new ProcessTallySubmissionCommand(request), cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Tally submission could not be enqueued.",
                detail: result.ErrorMessage);
        }

        return Accepted(result.Value);
    }
}
