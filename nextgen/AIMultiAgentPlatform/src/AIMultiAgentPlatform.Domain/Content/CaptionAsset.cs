namespace AIMultiAgentPlatform.Domain.Content;

public sealed record CaptionAsset(
    string CaptionAssetId,
    string DailyContentRequestId,
    string Caption,
    string EngagementPrompt,
    string CallToActionKeyword,
    IReadOnlyList<string> Hashtags);
