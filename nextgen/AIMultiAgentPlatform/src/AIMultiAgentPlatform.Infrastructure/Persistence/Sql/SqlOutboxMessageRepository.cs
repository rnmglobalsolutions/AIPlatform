using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.Messaging;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql;

public sealed class SqlOutboxMessageRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : IOutboxMessageRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(PendingOutboxCommand command, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.OutboxMessages.Add(new SqlOutboxMessageEntity
        {
            OutboxMessageId = command.OutboxMessageId,
            TenantId = command.Envelope.TenantId,
            CorrelationId = command.Envelope.CorrelationId,
            MessageType = command.Envelope.MessageType,
            EntityName = command.CommandName,
            PayloadJson = command.Envelope.PayloadJson,
            PropertiesJson = JsonSerializer.Serialize(command.Envelope.Properties, JsonOptions),
            CreatedUtc = command.CreatedUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingOutboxCommand>> GetPendingAsync(int maxCount, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await dbContext.OutboxMessages
            .Where(item => item.DispatchedUtc == null)
            .OrderBy(item => item.CreatedUtc)
            .Take(maxCount)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return entities
            .Select(entity => new PendingOutboxCommand(
                entity.OutboxMessageId,
                entity.EntityName,
                new MessageEnvelope(
                    entity.OutboxMessageId,
                    entity.CorrelationId,
                    entity.TenantId,
                    entity.MessageType,
                    entity.PayloadJson,
                    entity.CreatedUtc,
                    DeserializeProperties(entity.PropertiesJson)),
                entity.CreatedUtc))
            .ToArray();
    }

    public async Task MarkDispatchedAsync(string outboxMessageId, DateTime dispatchedUtc, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.OutboxMessages.SingleOrDefaultAsync(
            item => item.OutboxMessageId == outboxMessageId,
            cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.DispatchedUtc = dispatchedUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, string>? DeserializeProperties(string propertiesJson)
    {
        if (string.IsNullOrWhiteSpace(propertiesJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(propertiesJson, JsonOptions);
    }
}
