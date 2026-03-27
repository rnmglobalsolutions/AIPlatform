namespace AIMultiAgentPlatform.Domain.Content;

public sealed record RepurposedAssetBundle(
    string RepurposedAssetBundleId,
    string DailyContentRequestId,
    string CarouselOutline,
    IReadOnlyList<string> StoryFrames,
    string LinkedInPost,
    string QuotePost,
    string ShortClipIdea,
    IReadOnlyList<string> CommentHooks,
    IReadOnlyList<VideoClipExtractionPlan>? ClipPlans = null);
