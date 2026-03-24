using AIMultiAgentPlatform.Application.LeadGeneration;
using AIMultiAgentPlatform.Contracts.ManyChat;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/integrations/manychat/events")]
public sealed class ManyChatEventsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ProcessManyChatEventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostAsync(
        [FromBody] ProcessManyChatEventRequest request,
        [FromServices] ProcessManyChatEventUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new ProcessManyChatEventCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "ManyChat event could not be processed.",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(PostAsync), result.Value);
    }
}
