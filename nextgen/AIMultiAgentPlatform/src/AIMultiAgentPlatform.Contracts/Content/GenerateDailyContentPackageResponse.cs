namespace AIMultiAgentPlatform.Contracts.Content;

public sealed record GenerateDailyContentPackageResponse(
    string DailyContentRequestId,
    string DailyContentBriefId,
    string PrimaryAssetId,
    string CaptionAssetId,
    string RepurposedAssetBundleId,
    string PrimaryFormat);
