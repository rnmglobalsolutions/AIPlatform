namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql.Entities;

public sealed class SqlAggregateDocumentEntity
{
    public string AggregateType { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string LookupKey { get; set; } = string.Empty;
    public string LookupKey2 { get; set; } = string.Empty;
    public string LookupKey3 { get; set; } = string.Empty;
    public DateTime? SortUtc { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
