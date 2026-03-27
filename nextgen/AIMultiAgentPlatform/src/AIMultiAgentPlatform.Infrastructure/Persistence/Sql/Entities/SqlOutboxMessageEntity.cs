namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql.Entities;

public sealed class SqlOutboxMessageEntity
{
    public string OutboxMessageId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string PropertiesJson { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime? DispatchedUtc { get; set; }
}
