using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql;

public sealed class SqlInboxMessageRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : IInboxMessageRepository
{
    public async Task<bool> TryStartProcessingAsync(
        string messageId,
        string consumerName,
        string correlationId,
        string tenantId,
        string payloadJson,
        DateTime receivedUtc,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.InboxMessages.Add(new SqlInboxMessageEntity
        {
            MessageId = messageId,
            ConsumerName = consumerName,
            CorrelationId = correlationId,
            TenantId = tenantId,
            PayloadJson = payloadJson,
            ReceivedUtc = receivedUtc,
            ProcessedUtc = null
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public async Task MarkProcessedAsync(
        string messageId,
        string consumerName,
        DateTime processedUtc,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.InboxMessages.SingleOrDefaultAsync(
            item => item.MessageId == messageId && item.ConsumerName == consumerName,
            cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.ProcessedUtc = processedUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(
        string messageId,
        string consumerName,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.InboxMessages.SingleOrDefaultAsync(
            item => item.MessageId == messageId && item.ConsumerName == consumerName,
            cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.InboxMessages.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
