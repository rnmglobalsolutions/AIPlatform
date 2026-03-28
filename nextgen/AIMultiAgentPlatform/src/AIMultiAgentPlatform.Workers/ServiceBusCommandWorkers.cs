using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.DailyContent;
using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Application.Publishing;
using AIMultiAgentPlatform.Application.Orchestration;
using AIMultiAgentPlatform.Application.ReviewAndScheduling;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIMultiAgentPlatform.Workers;

public sealed class ServiceBusCommandWorkers(
    IServiceScopeFactory scopeFactory,
    ILogger<ServiceBusCommandWorkers> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Function("ProcessTallySubmissionCommandWorker")]
    public async Task ProcessTallySubmissionAsync(
        [ServiceBusTrigger("%ServiceBus:Commands:ProcessTallySubmissionEntityName%", Connection = "ServiceBus:ConnectionString")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        await ExecuteOnceAsync<ProcessTallySubmissionCommand>(
            message,
            "ProcessTallySubmissionCommandWorker",
            async (serviceProvider, command, token) =>
            {
                logger.LogInformation(
                    "Processing {CommandName} message {MessageId} for external submission {ExternalSubmissionId}.",
                    PlatformCommandNames.ProcessTallySubmission,
                    message.MessageId,
                    command.Submission.ExternalSubmissionId);

                var useCase = serviceProvider.GetRequiredService<ProcessTallySubmissionUseCase>();
                var result = await useCase.ExecuteAsync(command, token);
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Business failure while processing {PlatformCommandNames.ProcessTallySubmission}: {result.ErrorCode} - {result.ErrorMessage}");
                }
            },
            cancellationToken);
    }

    [Function("GenerateDailyContentPackageCommandWorker")]
    public async Task GenerateDailyContentPackageAsync(
        [ServiceBusTrigger("%ServiceBus:Commands:GenerateDailyContentPackageEntityName%", Connection = "ServiceBus:ConnectionString")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        await ExecuteOnceAsync<GenerateDailyContentPackageCommand>(
            message,
            "GenerateDailyContentPackageCommandWorker",
            async (serviceProvider, command, token) =>
            {
                logger.LogInformation(
                    "Processing {CommandName} message {MessageId} for tenant {TenantId} sequence {Sequence}.",
                    PlatformCommandNames.GenerateDailyContentPackage,
                    message.MessageId,
                    command.Request.TenantId,
                    command.Request.Sequence);

                var useCase = serviceProvider.GetRequiredService<GenerateDailyContentPackageUseCase>();
                var result = await useCase.ExecuteAsync(command, token);
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Business failure while processing {PlatformCommandNames.GenerateDailyContentPackage}: {result.ErrorCode} - {result.ErrorMessage}");
                }
            },
            cancellationToken);
    }

    [Function("ReviewAndScheduleDailyContentCommandWorker")]
    public async Task ReviewAndScheduleDailyContentAsync(
        [ServiceBusTrigger("%ServiceBus:Commands:ReviewAndScheduleDailyContentEntityName%", Connection = "ServiceBus:ConnectionString")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        await ExecuteOnceAsync<ReviewAndScheduleDailyContentCommand>(
            message,
            "ReviewAndScheduleDailyContentCommandWorker",
            async (serviceProvider, command, token) =>
            {
                logger.LogInformation(
                    "Processing {CommandName} message {MessageId} for tenant {TenantId} request {DailyContentRequestId}.",
                    PlatformCommandNames.ReviewAndScheduleDailyContent,
                    message.MessageId,
                    command.Request.TenantId,
                    command.Request.DailyContentRequestId);

                var useCase = serviceProvider.GetRequiredService<ReviewAndScheduleDailyContentUseCase>();
                var result = await useCase.ExecuteAsync(command, token);
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Business failure while processing {PlatformCommandNames.ReviewAndScheduleDailyContent}: {result.ErrorCode} - {result.ErrorMessage}");
                }
            },
            cancellationToken);
    }

    [Function("PublishScheduledContentCommandWorker")]
    public async Task PublishScheduledContentAsync(
        [ServiceBusTrigger("%ServiceBus:Commands:PublishScheduledContentEntityName%", Connection = "ServiceBus:ConnectionString")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        await ExecuteOnceAsync<PublishScheduledContentCommand>(
            message,
            "PublishScheduledContentCommandWorker",
            async (serviceProvider, command, token) =>
            {
                logger.LogInformation(
                    "Processing {CommandName} message {MessageId} for tenant {TenantId} scheduling job {SchedulingJobId}.",
                    PlatformCommandNames.PublishScheduledContent,
                    message.MessageId,
                    command.Request.TenantId,
                    command.Request.SchedulingJobId);

                var useCase = serviceProvider.GetRequiredService<PublishScheduledContentUseCase>();
                var result = await useCase.ExecuteAsync(command, token);
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Business failure while processing {PlatformCommandNames.PublishScheduledContent}: {result.ErrorCode} - {result.ErrorMessage}");
                }
            },
            cancellationToken);
    }

    private async Task ExecuteOnceAsync<TCommand>(
        ServiceBusReceivedMessage message,
        string consumerName,
        Func<IServiceProvider, TCommand, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var inboxRepository = serviceProvider.GetRequiredService<IInboxMessageRepository>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var command = DeserializeMessage<TCommand>(message);
        var tenantId = ResolveTenantId(message);
        var payloadJson = message.Body.ToString();

        var claimed = await inboxRepository.TryStartProcessingAsync(
            message.MessageId,
            consumerName,
            message.CorrelationId ?? string.Empty,
            tenantId,
            payloadJson,
            clock.UtcNow,
            cancellationToken);

        if (!claimed)
        {
            logger.LogInformation(
                "Skipping duplicate Service Bus message {MessageId} for consumer {ConsumerName}.",
                message.MessageId,
                consumerName);
            return;
        }

        try
        {
            using (logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = message.CorrelationId ?? string.Empty,
                       ["MessageId"] = message.MessageId,
                       ["ConsumerName"] = consumerName,
                       ["TenantId"] = tenantId
                   }))
            {
                await handler(serviceProvider, command, cancellationToken);
                await inboxRepository.MarkProcessedAsync(message.MessageId, consumerName, clock.UtcNow, cancellationToken);
            }
        }
        catch
        {
            await inboxRepository.ReleaseAsync(message.MessageId, consumerName, cancellationToken);
            throw;
        }
    }

    private static T DeserializeMessage<T>(ServiceBusReceivedMessage message)
    {
        var value = JsonSerializer.Deserialize<T>(message.Body, JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException($"Service Bus message {message.MessageId} could not be deserialized into {typeof(T).Name}.");
        }

        return value;
    }

    private static string ResolveTenantId(ServiceBusReceivedMessage message)
    {
        if (message.ApplicationProperties.TryGetValue("tenantId", out var tenantId) && tenantId is not null)
        {
            return tenantId.ToString() ?? string.Empty;
        }

        return string.Empty;
    }
}
