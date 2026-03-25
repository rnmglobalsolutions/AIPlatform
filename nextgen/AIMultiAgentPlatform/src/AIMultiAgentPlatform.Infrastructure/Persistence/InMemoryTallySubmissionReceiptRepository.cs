using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Intake;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryTallySubmissionReceiptRepository : ITallySubmissionReceiptRepository
{
    private readonly ConcurrentDictionary<string, TallySubmissionReceipt> _items = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(TallySubmissionReceipt receipt, CancellationToken cancellationToken)
    {
        _items[Normalize(receipt.ExternalSubmissionId)] = receipt;
        return Task.CompletedTask;
    }

    public Task<TallySubmissionReceipt?> FindByExternalSubmissionIdAsync(string externalSubmissionId, CancellationToken cancellationToken) =>
        Task.FromResult(Find(externalSubmissionId));

    public TallySubmissionReceipt? Find(string externalSubmissionId) =>
        _items.TryGetValue(Normalize(externalSubmissionId), out var receipt) ? receipt : null;

    private static string Normalize(string externalSubmissionId) => externalSubmissionId.Trim();
}
