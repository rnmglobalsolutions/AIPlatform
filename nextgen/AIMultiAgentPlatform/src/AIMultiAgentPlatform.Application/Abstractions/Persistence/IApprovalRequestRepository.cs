using AIMultiAgentPlatform.Domain.Reviewing;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface IApprovalRequestRepository
{
    Task SaveAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken);

    Task<ApprovalRequest?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken);
}
