using AIMultiAgentPlatform.Application.Voice;
using AIMultiAgentPlatform.Contracts.Voice;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/agents/voice")]
public sealed class VoiceAgentController : ControllerBase
{
    [HttpPost("orchestrate")]
    [ProducesResponseType(typeof(OrchestrateVoiceAgentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OrchestrateAsync(
        [FromBody] OrchestrateVoiceAgentRequest request,
        [FromServices] OrchestrateVoiceAgentUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new OrchestrateVoiceAgentCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Voice orchestration could not be completed.",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(OrchestrateAsync), result.Value);
    }
}
