using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Orchestration;
using AIMultiAgentPlatform.Contracts.Orchestration;

namespace AIMultiAgentPlatform.Application.Publishing;

public sealed class EnqueuePublishScheduledContentUseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICommandEnqueuer _commandEnqueuer;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public EnqueuePublishScheduledContentUseCase(
        ICommandEnqueuer commandEnqueuer,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _commandEnqueuer = commandEnqueuer;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<CommandEnqueueResponse>> ExecuteAsync(
        PublishScheduledContentCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Request.TenantId) || string.IsNullOrWhiteSpace(command.Request.SchedulingJobId))
        {
            return Result<CommandEnqueueResponse>.Failure(
                "publishing.job.invalid",
                "TenantId and SchedulingJobId are required for asynchronous publishing.");
        }

        var correlationId = string.IsNullOrWhiteSpace(command.CorrelationId)
            ? $"publish-{command.Request.TenantId.Trim()}-{command.Request.SchedulingJobId.Trim()}"
            : command.CorrelationId.Trim();
        var messageId = _idGenerator.NewId("cmd");
        var acceptedUtc = _clock.UtcNow;

        var envelope = new MessageEnvelope(
            messageId,
            correlationId,
            command.Request.TenantId.Trim(),
            nameof(PublishScheduledContentCommand),
            JsonSerializer.Serialize(command with { CorrelationId = correlationId }, JsonOptions),
            acceptedUtc,
            new Dictionary<string, string>
            {
                ["schedulingJobId"] = command.Request.SchedulingJobId.Trim()
            });

        await _commandEnqueuer.EnqueueAsync(PlatformCommandNames.PublishScheduledContent, envelope, cancellationToken);

        return Result<CommandEnqueueResponse>.Success(
            new CommandEnqueueResponse(
                messageId,
                correlationId,
                PlatformCommandNames.PublishScheduledContent,
                command.Request.TenantId.Trim(),
                acceptedUtc));
    }
}
