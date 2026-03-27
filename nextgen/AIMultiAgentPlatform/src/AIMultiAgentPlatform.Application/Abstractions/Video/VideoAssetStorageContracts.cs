namespace AIMultiAgentPlatform.Application.Abstractions.Video;

public sealed record VideoAssetStorageRequest(
    string TenantId,
    string DailyContentRequestId,
    string VideoGenerationJobId,
    string ProviderName,
    string ProviderVideoUrl,
    string SuggestedFileName);

public sealed record VideoAssetStorageResult(
    bool Succeeded,
    string VideoUrl,
    string StorageProvider,
    string FailureReason)
{
    public static VideoAssetStorageResult Success(string videoUrl, string storageProvider) =>
        new(true, videoUrl, storageProvider, string.Empty);

    public static VideoAssetStorageResult Failure(string storageProvider, string failureReason) =>
        new(false, string.Empty, storageProvider, failureReason);
}
