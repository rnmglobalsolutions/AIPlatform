using AIMultiAgentPlatform.Domain.Intake;

namespace AIMultiAgentPlatform.Application.Abstractions.Persistence;

public interface ITallySubmissionReceiptRepository
{
    Task SaveAsync(TallySubmissionReceipt receipt, CancellationToken cancellationToken);

    Task<TallySubmissionReceipt?> FindByExternalSubmissionIdAsync(string externalSubmissionId, CancellationToken cancellationToken);
}
