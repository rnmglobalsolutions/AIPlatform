using System.Text.Json;
using AIMultiAgentPlatform.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql;

public abstract class SqlAggregateDocumentRepositoryBase
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<AiPlatformDbContext> _dbContextFactory;

    protected SqlAggregateDocumentRepositoryBase(IDbContextFactory<AiPlatformDbContext> dbContextFactory) =>
        _dbContextFactory = dbContextFactory;

    protected async Task SaveDocumentAsync<TAggregate>(
        string aggregateType,
        string documentId,
        string tenantId,
        TAggregate aggregate,
        DateTime createdUtc,
        string lookupKey = "",
        string lookupKey2 = "",
        string lookupKey3 = "",
        DateTime? sortUtc = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.AggregateDocuments
            .SingleOrDefaultAsync(
                item => item.AggregateType == aggregateType && item.DocumentId == documentId,
                cancellationToken);

        if (entity is null)
        {
            entity = new SqlAggregateDocumentEntity
            {
                AggregateType = aggregateType,
                DocumentId = documentId,
                CreatedUtc = createdUtc
            };

            dbContext.AggregateDocuments.Add(entity);
        }

        entity.TenantId = Normalize(tenantId);
        entity.LookupKey = Normalize(lookupKey);
        entity.LookupKey2 = Normalize(lookupKey2);
        entity.LookupKey3 = Normalize(lookupKey3);
        entity.SortUtc = sortUtc;
        entity.PayloadJson = JsonSerializer.Serialize(aggregate, SerializerOptions);
        entity.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    protected async Task<TAggregate?> FindByIdAsync<TAggregate>(
        string aggregateType,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.AggregateDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AggregateType == aggregateType && item.DocumentId == documentId,
                cancellationToken);

        return Deserialize<TAggregate>(entity);
    }

    protected async Task<TAggregate?> FindFirstAsync<TAggregate>(
        string aggregateType,
        Func<IQueryable<SqlAggregateDocumentEntity>, IQueryable<SqlAggregateDocumentEntity>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await queryBuilder(dbContext.AggregateDocuments.AsNoTracking().Where(item => item.AggregateType == aggregateType))
            .FirstOrDefaultAsync(cancellationToken);

        return Deserialize<TAggregate>(entity);
    }

    protected async Task<IReadOnlyList<TAggregate>> ListAsync<TAggregate>(
        string aggregateType,
        Func<IQueryable<SqlAggregateDocumentEntity>, IQueryable<SqlAggregateDocumentEntity>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await queryBuilder(dbContext.AggregateDocuments.AsNoTracking().Where(item => item.AggregateType == aggregateType))
            .ToListAsync(cancellationToken);

        return entities
            .Select(Deserialize<TAggregate>)
            .Where(static item => item is not null)
            .Cast<TAggregate>()
            .ToArray();
    }

    protected async Task DeleteDocumentsAsync(
        string aggregateType,
        Func<IQueryable<SqlAggregateDocumentEntity>, IQueryable<SqlAggregateDocumentEntity>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await queryBuilder(dbContext.AggregateDocuments.Where(item => item.AggregateType == aggregateType))
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return;
        }

        dbContext.AggregateDocuments.RemoveRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TAggregate? Deserialize<TAggregate>(SqlAggregateDocumentEntity? entity)
    {
        if (entity is null || string.IsNullOrWhiteSpace(entity.PayloadJson))
        {
            return default;
        }

        return JsonSerializer.Deserialize<TAggregate>(entity.PayloadJson, SerializerOptions);
    }

    protected static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
