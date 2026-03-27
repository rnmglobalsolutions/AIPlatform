using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Reviewing;
using Microsoft.EntityFrameworkCore;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql;

public sealed class SqlQualityReviewRepository : SqlAggregateDocumentRepositoryBase, IQualityReviewRepository
{
    private const string AggregateType = "QualityReview";

    public SqlQualityReviewRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(QualityReview review, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            review.QualityReviewId,
            review.TenantId.Value,
            review,
            review.ReviewedUtc,
            lookupKey: Normalize(review.DailyContentRequestId),
            sortUtc: review.ReviewedUtc,
            cancellationToken: cancellationToken);

    public Task<QualityReview?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        FindFirstAsync<QualityReview>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(requestId)),
            cancellationToken);
}

public sealed class SqlComplianceReviewRepository : SqlAggregateDocumentRepositoryBase, IComplianceReviewRepository
{
    private const string AggregateType = "ComplianceReview";

    public SqlComplianceReviewRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(ComplianceReview review, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            review.ComplianceReviewId,
            review.TenantId.Value,
            review,
            review.ReviewedUtc,
            lookupKey: Normalize(review.DailyContentRequestId),
            lookupKey2: review.RiskLevel.ToString(),
            sortUtc: review.ReviewedUtc,
            cancellationToken: cancellationToken);

    public Task<ComplianceReview?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        FindFirstAsync<ComplianceReview>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(requestId)),
            cancellationToken);
}

public sealed class SqlApprovalRequestRepository : SqlAggregateDocumentRepositoryBase, IApprovalRequestRepository
{
    private const string AggregateType = "ApprovalRequest";

    public SqlApprovalRequestRepository(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            approvalRequest.ApprovalRequestId,
            approvalRequest.TenantId.Value,
            approvalRequest,
            approvalRequest.ReviewedUtc,
            lookupKey: Normalize(approvalRequest.DailyContentRequestId),
            lookupKey2: approvalRequest.Status.ToString(),
            sortUtc: approvalRequest.ReviewedUtc,
            cancellationToken: cancellationToken);

    public Task<ApprovalRequest?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
        FindFirstAsync<ApprovalRequest>(
            AggregateType,
            query => query.Where(item => item.LookupKey == Normalize(requestId)),
            cancellationToken);
}
