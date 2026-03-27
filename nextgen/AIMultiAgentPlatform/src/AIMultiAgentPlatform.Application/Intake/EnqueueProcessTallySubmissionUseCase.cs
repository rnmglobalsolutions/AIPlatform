using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Orchestration;
using AIMultiAgentPlatform.Contracts.Orchestration;

namespace AIMultiAgentPlatform.Application.Intake;

public sealed class EnqueueProcessTallySubmissionUseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICommandEnqueuer _commandEnqueuer;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public EnqueueProcessTallySubmissionUseCase(
        ICommandEnqueuer commandEnqueuer,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _commandEnqueuer = commandEnqueuer;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<CommandEnqueueResponse>> ExecuteAsync(
        ProcessTallySubmissionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Submission.ExternalSubmissionId))
        {
            return Result<CommandEnqueueResponse>.Failure(
                "intake.external-submission-id.required",
                "ExternalSubmissionId is required for asynchronous intake processing.");
        }

        if (string.IsNullOrWhiteSpace(command.Submission.BusinessName))
        {
            return Result<CommandEnqueueResponse>.Failure(
                "intake.business-name.required",
                "Business name is required.");
        }

        var correlationId = string.IsNullOrWhiteSpace(command.CorrelationId)
            ? $"tally-{command.Submission.ExternalSubmissionId.Trim()}"
            : command.CorrelationId.Trim();
        var messageId = _idGenerator.NewId("cmd");
        var acceptedUtc = _clock.UtcNow;

        var envelope = new MessageEnvelope(
            messageId,
            correlationId,
            TenantId: string.Empty,
            nameof(ProcessTallySubmissionCommand),
            JsonSerializer.Serialize(command with { CorrelationId = correlationId }, JsonOptions),
            acceptedUtc,
            new Dictionary<string, string>
            {
                ["externalSubmissionId"] = command.Submission.ExternalSubmissionId.Trim()
            });

        await _commandEnqueuer.EnqueueAsync(PlatformCommandNames.ProcessTallySubmission, envelope, cancellationToken);

        return Result<CommandEnqueueResponse>.Success(
            new CommandEnqueueResponse(
                messageId,
                correlationId,
                PlatformCommandNames.ProcessTallySubmission,
                string.Empty,
                acceptedUtc));
    }
}
