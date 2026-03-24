using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Contracts.Intake;
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
}
