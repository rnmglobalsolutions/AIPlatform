using AIMultiAgentPlatform.Application.Booking;
using AIMultiAgentPlatform.Contracts.Booking;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/agents/booking")]
public sealed class BookingAgentController : ControllerBase
{
    [HttpPost("orchestrate")]
    [ProducesResponseType(typeof(OrchestrateBookingAgentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OrchestrateAsync(
        [FromBody] OrchestrateBookingAgentRequest request,
        [FromServices] OrchestrateBookingAgentUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new OrchestrateBookingAgentCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Booking orchestration could not be completed.",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(OrchestrateAsync), result.Value);
    }
}
