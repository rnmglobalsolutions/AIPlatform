using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Application.Orchestration;
using AIMultiAgentPlatform.Contracts.Orchestration;

namespace AIMultiAgentPlatform.Application.DailyContent;

public sealed class EnqueueGenerateDailyContentPackageUseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICommandEnqueuer _commandEnqueuer;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public EnqueueGenerateDailyContentPackageUseCase(
        ICommandEnqueuer commandEnqueuer,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _commandEnqueuer = commandEnqueuer;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<CommandEnqueueResponse>> ExecuteAsync(
        GenerateDailyContentPackageCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Request.TenantId))
        {
            return Result<CommandEnqueueResponse>.Failure(
                "daily-content.tenant.required",
                "TenantId is required for asynchronous daily content generation.");
        }

        if (string.IsNullOrWhiteSpace(command.Request.EditorialBacklogId))
        {
            return Result<CommandEnqueueResponse>.Failure(
                "daily-content.backlog.required",
                "EditorialBacklogId is required for asynchronous daily content generation.");
        }

        var correlationId = string.IsNullOrWhiteSpace(command.CorrelationId)
            ? $"daily-{command.Request.TenantId.Trim()}-{command.Request.Sequence}"
            : command.CorrelationId.Trim();
        var messageId = _idGenerator.NewId("cmd");
        var acceptedUtc = _clock.UtcNow;

        var envelope = new MessageEnvelope(
            messageId,
            correlationId,
            command.Request.TenantId.Trim(),
            nameof(GenerateDailyContentPackageCommand),
            JsonSerializer.Serialize(command with { CorrelationId = correlationId }, JsonOptions),
            acceptedUtc,
            new Dictionary<string, string>
            {
                ["editorialBacklogId"] = command.Request.EditorialBacklogId.Trim(),
                ["sequence"] = command.Request.Sequence.ToString()
            });

        await _commandEnqueuer.EnqueueAsync(PlatformCommandNames.GenerateDailyContentPackage, envelope, cancellationToken);

        return Result<CommandEnqueueResponse>.Success(
            new CommandEnqueueResponse(
                messageId,
                correlationId,
                PlatformCommandNames.GenerateDailyContentPackage,
                command.Request.TenantId.Trim(),
                acceptedUtc));
    }
}
