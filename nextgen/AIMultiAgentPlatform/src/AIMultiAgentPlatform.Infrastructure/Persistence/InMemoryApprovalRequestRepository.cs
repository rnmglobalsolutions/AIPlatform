using System.Collections.Concurrent;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Reviewing;

namespace AIMultiAgentPlatform.Infrastructure.Persistence;

public sealed class InMemoryApprovalRequestRepository : IApprovalRequestRepository
{
    private readonly ConcurrentDictionary<string, ApprovalRequest> _items = new(StringComparer.Ordinal);

    public Task SaveAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken)
    {
        _items[approvalRequest.ApprovalRequestId] = approvalRequest;
        return Task.CompletedTask;
    }

    public Task<ApprovalRequest?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.FirstOrDefault(item => item.DailyContentRequestId == requestId));

    public ApprovalRequest? Find(string approvalRequestId) => _items.TryGetValue(approvalRequestId, out var approvalRequest) ? approvalRequest : null;

    public IReadOnlyList<ApprovalRequest> ListAll() => _items.Values.ToArray();
}
