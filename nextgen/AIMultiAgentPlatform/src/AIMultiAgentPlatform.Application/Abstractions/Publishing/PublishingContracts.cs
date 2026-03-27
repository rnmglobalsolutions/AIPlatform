namespace AIMultiAgentPlatform.Application.Abstractions.Publishing;

public sealed record PublishingRequest(
    string TenantId,
    string CorrelationId,
    string ExternalProfileId,
    string AccessToken,
    string Platform,
    string Caption,
    string AssetUrl,
    DateTime PublishAtUtc,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record PublishingResult(
    bool Succeeded,
    string Platform,
    string ExternalPostId,
    string ExternalUrl,
    string FailureReason)
{
    public static PublishingResult Success(
        string platform,
        string externalPostId,
        string externalUrl) =>
        new(true, platform, externalPostId, externalUrl, string.Empty);

    public static PublishingResult Failure(
        string platform,
        string failureReason) =>
        new(false, platform, string.Empty, string.Empty, failureReason);
}

public sealed record PublishingReconciliationRequest(
    string TenantId,
    string ExternalProfileId,
    string AccessToken,
    string Platform,
    string ExternalPostId,
    string ExistingExternalUrl);

public sealed record PublishingMetrics(
    long Reach,
    long Clicks,
    long Likes,
    long Comments,
    long Shares);

public sealed record PublishingReconciliationResult(
    bool Succeeded,
    string Platform,
    string ProviderStatus,
    string ExternalUrl,
    PublishingMetrics Metrics,
    DateTime? PublishedUtc,
    string FailureReason)
{
    public static PublishingReconciliationResult Success(
        string platform,
        string providerStatus,
        string externalUrl,
        PublishingMetrics metrics,
        DateTime? publishedUtc = null) =>
        new(true, platform, providerStatus, externalUrl, metrics, publishedUtc, string.Empty);

    public static PublishingReconciliationResult Failure(
        string platform,
        string failureReason) =>
        new(false, platform, string.Empty, string.Empty, new PublishingMetrics(0, 0, 0, 0, 0), null, failureReason);
}
