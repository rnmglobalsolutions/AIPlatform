using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Contracts.Reporting;
using Microsoft.AspNetCore.Mvc;

namespace AIMultiAgentPlatform.Api.Controllers;

[ApiController]
[Route("api/reporting/monthly-snapshots")]
public sealed class ReportingController : ControllerBase
{
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateMonthlyPerformanceSnapshotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateAsync(
        [FromBody] GenerateMonthlyPerformanceSnapshotRequest request,
        [FromServices] GenerateMonthlyPerformanceSnapshotUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new GenerateMonthlyPerformanceSnapshotCommand(request, request.CorrelationId ?? string.Empty),
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return ValidationProblem(
                title: "Monthly performance snapshot could not be generated.",
                detail: result.ErrorMessage);
        }

        return CreatedAtAction(nameof(GenerateAsync), result.Value);
    }
}
