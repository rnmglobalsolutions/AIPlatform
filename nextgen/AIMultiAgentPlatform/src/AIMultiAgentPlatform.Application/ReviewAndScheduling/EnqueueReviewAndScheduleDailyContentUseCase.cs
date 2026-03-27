using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Orchestration;
using AIMultiAgentPlatform.Contracts.Orchestration;

namespace AIMultiAgentPlatform.Application.ReviewAndScheduling;

public sealed class EnqueueReviewAndScheduleDailyContentUseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICommandEnqueuer _commandEnqueuer;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public EnqueueReviewAndScheduleDailyContentUseCase(
        ICommandEnqueuer commandEnqueuer,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _commandEnqueuer = commandEnqueuer;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<CommandEnqueueResponse>> ExecuteAsync(
        ReviewAndScheduleDailyContentCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Request.TenantId))
        {
            return Result<CommandEnqueueResponse>.Failure("review.tenant.required", "TenantId is required for asynchronous review and scheduling.");
        }

        if (string.IsNullOrWhiteSpace(command.Request.DailyContentRequestId))
        {
            return Result<CommandEnqueueResponse>.Failure("review.request.required", "DailyContentRequestId is required for asynchronous review and scheduling.");
        }

        var correlationId = string.IsNullOrWhiteSpace(command.CorrelationId)
            ? $"review-{command.Request.TenantId.Trim()}-{command.Request.DailyContentRequestId.Trim()}"
            : command.CorrelationId.Trim();
        var messageId = _idGenerator.NewId("cmd");
        var acceptedUtc = _clock.UtcNow;

        var envelope = new MessageEnvelope(
            messageId,
            correlationId,
            command.Request.TenantId.Trim(),
            nameof(ReviewAndScheduleDailyContentCommand),
            JsonSerializer.Serialize(command with { CorrelationId = correlationId }, JsonOptions),
            acceptedUtc,
            new Dictionary<string, string>
            {
                ["dailyContentRequestId"] = command.Request.DailyContentRequestId.Trim()
            });

        await _commandEnqueuer.EnqueueAsync(PlatformCommandNames.ReviewAndScheduleDailyContent, envelope, cancellationToken);

        return Result<CommandEnqueueResponse>.Success(
            new CommandEnqueueResponse(
                messageId,
                correlationId,
                PlatformCommandNames.ReviewAndScheduleDailyContent,
                command.Request.TenantId.Trim(),
                acceptedUtc));
    }
}
