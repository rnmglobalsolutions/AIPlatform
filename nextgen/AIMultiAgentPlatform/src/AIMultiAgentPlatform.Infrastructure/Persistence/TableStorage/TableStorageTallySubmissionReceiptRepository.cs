using System.Security.Cryptography;
using System.Text;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Intake;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Azure.Data.Tables;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.TableStorage;

internal sealed class TableStorageTallySubmissionReceiptRepository : TableStorageJsonRepositoryBase, ITallySubmissionReceiptRepository
{
    public TableStorageTallySubmissionReceiptRepository(TableServiceClient tableServiceClient, TableStorageOptions options)
        : base(tableServiceClient.GetTableClient(options.TallySubmissionReceiptTableName))
    {
    }

    protected override string PartitionKey => "TallySubmissionReceipt";

    public Task SaveAsync(TallySubmissionReceipt receipt, CancellationToken cancellationToken) =>
        SaveDocumentAsync(BuildRowKey(receipt.ExternalSubmissionId), receipt, cancellationToken);

    public Task<TallySubmissionReceipt?> FindByExternalSubmissionIdAsync(string externalSubmissionId, CancellationToken cancellationToken) =>
        FindDocumentAsync<TallySubmissionReceipt>(BuildRowKey(externalSubmissionId), cancellationToken);

    private static string BuildRowKey(string externalSubmissionId)
    {
        var normalized = externalSubmissionId.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
