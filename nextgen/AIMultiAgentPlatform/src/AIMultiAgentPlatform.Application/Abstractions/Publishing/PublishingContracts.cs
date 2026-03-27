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
