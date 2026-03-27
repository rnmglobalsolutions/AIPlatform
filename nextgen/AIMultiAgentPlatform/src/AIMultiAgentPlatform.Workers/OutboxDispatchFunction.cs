using AIMultiAgentPlatform.Application.Orchestration;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIMultiAgentPlatform.Workers;

public sealed class OutboxDispatchFunction(
    IServiceScopeFactory scopeFactory,
    OutboxOptions options,
    ILogger<OutboxDispatchFunction> logger)
{
    [Function("DispatchPendingOutboxCommands")]
    public async Task RunAsync(
        [TimerTrigger("%Outbox:DispatchSchedule%")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<DispatchPendingOutboxCommandsUseCase>();
        var result = await useCase.ExecuteAsync(options.DispatchBatchSize, cancellationToken);

        logger.LogInformation(
            "Outbox dispatcher processed {AttemptedCount} pending commands and dispatched {DispatchedCount}.",
            result.AttemptedCount,
            result.DispatchedCount);
    }
}
