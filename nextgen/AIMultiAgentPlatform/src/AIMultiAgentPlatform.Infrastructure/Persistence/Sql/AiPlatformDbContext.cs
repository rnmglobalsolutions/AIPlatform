using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql;

public sealed class AiPlatformDbContext : DbContext
{
    private readonly SqlOptions _sqlOptions;

    public AiPlatformDbContext(
        DbContextOptions<AiPlatformDbContext> options,
        SqlOptions sqlOptions)
        : base(options)
    {
        _sqlOptions = sqlOptions;
    }

    public DbSet<SqlOutboxMessageEntity> OutboxMessages => Set<SqlOutboxMessageEntity>();

    public DbSet<SqlInboxMessageEntity> InboxMessages => Set<SqlInboxMessageEntity>();

    public DbSet<SqlAggregateDocumentEntity> AggregateDocuments => Set<SqlAggregateDocumentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(_sqlOptions.Schema);

        modelBuilder.Entity<SqlOutboxMessageEntity>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(item => item.OutboxMessageId);
            entity.Property(item => item.OutboxMessageId).HasMaxLength(128);
            entity.Property(item => item.TenantId).HasMaxLength(128);
            entity.Property(item => item.CorrelationId).HasMaxLength(128);
            entity.Property(item => item.MessageType).HasMaxLength(200);
            entity.Property(item => item.EntityName).HasMaxLength(200);
            entity.Property(item => item.PayloadJson).HasColumnType("nvarchar(max)");
            entity.Property(item => item.PropertiesJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(item => new { item.EntityName, item.DispatchedUtc });
        });

        modelBuilder.Entity<SqlInboxMessageEntity>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(item => new { item.MessageId, item.ConsumerName });
            entity.Property(item => item.MessageId).HasMaxLength(128);
            entity.Property(item => item.ConsumerName).HasMaxLength(200);
            entity.Property(item => item.CorrelationId).HasMaxLength(128);
            entity.Property(item => item.TenantId).HasMaxLength(128);
            entity.Property(item => item.PayloadJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(item => item.ProcessedUtc);
        });

        modelBuilder.Entity<SqlAggregateDocumentEntity>(entity =>
        {
            entity.ToTable("AggregateDocuments");
            entity.HasKey(item => new { item.AggregateType, item.DocumentId });
            entity.Property(item => item.AggregateType).HasMaxLength(128);
            entity.Property(item => item.DocumentId).HasMaxLength(128);
            entity.Property(item => item.TenantId).HasMaxLength(128);
            entity.Property(item => item.LookupKey).HasMaxLength(256);
            entity.Property(item => item.LookupKey2).HasMaxLength(256);
            entity.Property(item => item.LookupKey3).HasMaxLength(256);
            entity.Property(item => item.PayloadJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(item => new { item.AggregateType, item.TenantId, item.LookupKey });
            entity.HasIndex(item => new { item.AggregateType, item.TenantId, item.LookupKey2 });
            entity.HasIndex(item => new { item.AggregateType, item.TenantId, item.LookupKey3 });
            entity.HasIndex(item => new { item.AggregateType, item.LookupKey });
            entity.HasIndex(item => new { item.AggregateType, item.LookupKey2 });
            entity.HasIndex(item => new { item.AggregateType, item.LookupKey3 });
            entity.HasIndex(item => new { item.AggregateType, item.TenantId, item.SortUtc });
        });
    }
}
