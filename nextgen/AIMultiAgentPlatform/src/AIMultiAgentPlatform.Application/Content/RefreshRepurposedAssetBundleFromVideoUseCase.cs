using System.Text.RegularExpressions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Domain.Content;

namespace AIMultiAgentPlatform.Application.Content;

public sealed partial class RefreshRepurposedAssetBundleFromVideoUseCase
{
    private readonly IGeneratedVideoAssetRepository _generatedVideoAssetRepository;
    private readonly IRepurposedAssetBundleRepository _repurposedAssetBundleRepository;
    private readonly IPrimaryAssetRepository _primaryAssetRepository;
    private readonly ICaptionAssetRepository _captionAssetRepository;

    public RefreshRepurposedAssetBundleFromVideoUseCase(
        IGeneratedVideoAssetRepository generatedVideoAssetRepository,
        IRepurposedAssetBundleRepository repurposedAssetBundleRepository,
        IPrimaryAssetRepository primaryAssetRepository,
        ICaptionAssetRepository captionAssetRepository)
    {
        _generatedVideoAssetRepository = generatedVideoAssetRepository;
        _repurposedAssetBundleRepository = repurposedAssetBundleRepository;
        _primaryAssetRepository = primaryAssetRepository;
        _captionAssetRepository = captionAssetRepository;
    }

    public async Task<Result<RefreshRepurposedAssetBundleFromVideoResponse>> ExecuteAsync(
        RefreshRepurposedAssetBundleFromVideoRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DailyContentRequestId))
        {
            return Result<RefreshRepurposedAssetBundleFromVideoResponse>.Failure(
                "video-repurpose.request.required",
                "DailyContentRequestId is required.");
        }

        var generatedVideoAsset = await _generatedVideoAssetRepository.FindByRequestIdAsync(request.DailyContentRequestId, cancellationToken);
        if (generatedVideoAsset is null)
        {
            return Result<RefreshRepurposedAssetBundleFromVideoResponse>.Failure(
                "video-repurpose.asset.not-found",
                "Generated video asset was not found.");
        }

        var existingBundle = await _repurposedAssetBundleRepository.FindByRequestIdAsync(request.DailyContentRequestId, cancellationToken);
        if (existingBundle is null)
        {
            return Result<RefreshRepurposedAssetBundleFromVideoResponse>.Failure(
                "video-repurpose.bundle.not-found",
                "Repurposed asset bundle was not found.");
        }

        var primaryAsset = await _primaryAssetRepository.FindByRequestIdAsync(request.DailyContentRequestId, cancellationToken);
        if (primaryAsset is null)
        {
            return Result<RefreshRepurposedAssetBundleFromVideoResponse>.Failure(
                "video-repurpose.primary-asset.not-found",
                "Primary asset was not found.");
        }

        var captionAsset = await _captionAssetRepository.FindByRequestIdAsync(request.DailyContentRequestId, cancellationToken);
        var transcriptSentences = BuildTranscriptSentences(generatedVideoAsset, primaryAsset);
        var clipPlans = BuildClipPlans(generatedVideoAsset, transcriptSentences);
        var refreshedBundle = existingBundle with
        {
            CarouselOutline = BuildCarouselOutline(transcriptSentences, captionAsset?.CallToActionKeyword ?? "BOOK"),
            StoryFrames = BuildStoryFrames(transcriptSentences, primaryAsset.CallToAction),
            LinkedInPost = BuildLinkedInPost(transcriptSentences, primaryAsset.CallToAction),
            QuotePost = BuildQuotePost(transcriptSentences, primaryAsset),
            ShortClipIdea = BuildShortClipIdea(clipPlans, transcriptSentences, generatedVideoAsset),
            CommentHooks = BuildCommentHooks(transcriptSentences, captionAsset?.CallToActionKeyword ?? "BOOK"),
            ClipPlans = clipPlans
        };

        await _repurposedAssetBundleRepository.SaveAsync(refreshedBundle, cancellationToken);

        return Result<RefreshRepurposedAssetBundleFromVideoResponse>.Success(
            new RefreshRepurposedAssetBundleFromVideoResponse(
                refreshedBundle.RepurposedAssetBundleId,
                generatedVideoAsset.GeneratedVideoAssetId,
                transcriptSentences.Count));
    }

    private static IReadOnlyList<string> BuildTranscriptSentences(GeneratedVideoAsset generatedVideoAsset, PrimaryAsset primaryAsset) =>
        generatedVideoAsset.TranscriptSegments is { Count: > 0 }
            ? generatedVideoAsset.TranscriptSegments
                .Select(static segment => segment.Text)
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .Take(6)
                .ToArray()
            : BuildTranscriptSentences(generatedVideoAsset.TranscriptText, primaryAsset);

    private static IReadOnlyList<string> BuildTranscriptSentences(string transcriptText, PrimaryAsset primaryAsset)
    {
        var sourceText = string.IsNullOrWhiteSpace(transcriptText)
            ? $"{primaryAsset.Hook} {primaryAsset.Body} {primaryAsset.Payoff} {primaryAsset.CallToAction}"
            : transcriptText;

        var sentences = SentenceSplitter()
            .Split(sourceText)
            .Select(static sentence => sentence.Trim())
            .Where(static sentence => !string.IsNullOrWhiteSpace(sentence))
            .Select(static sentence => sentence.EndsWith(".", StringComparison.Ordinal) ||
                                       sentence.EndsWith("!", StringComparison.Ordinal) ||
                                       sentence.EndsWith("?", StringComparison.Ordinal)
                ? sentence
                : $"{sentence}.")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        return sentences.Length == 0 ? [$"{primaryAsset.Hook}."] : sentences;
    }

    private static string BuildCarouselOutline(IReadOnlyList<string> transcriptSentences, string callToActionKeyword)
    {
        var slides = transcriptSentences.Take(4).ToArray();
        return string.Join(
            "\n",
            slides.Select((sentence, index) => $"Slide {index + 1}: {sentence}")
                .Append($"Slide {slides.Length + 1}: CTA -> Comment or DM '{callToActionKeyword}'."));
    }

    private static IReadOnlyList<string> BuildStoryFrames(IReadOnlyList<string> transcriptSentences, string callToAction) =>
    [
        $"Frame 1: {transcriptSentences[0]}",
        $"Frame 2: {transcriptSentences[Math.Min(1, transcriptSentences.Count - 1)]}",
        $"Frame 3: CTA -> {callToAction}"
    ];

    private static string BuildLinkedInPost(IReadOnlyList<string> transcriptSentences, string callToAction)
    {
        var body = string.Join("\n\n", transcriptSentences.Take(3));
        return $"{body}\n\n{callToAction}";
    }

    private static string BuildQuotePost(IReadOnlyList<string> transcriptSentences, PrimaryAsset primaryAsset)
    {
        var quoteCandidate = transcriptSentences
            .OrderByDescending(static sentence => sentence.Length >= 45 && sentence.Length <= 140)
            .ThenByDescending(static sentence => sentence.Length)
            .FirstOrDefault();

        return $"\"{quoteCandidate ?? primaryAsset.Payoff}\"";
    }

    private static string BuildShortClipIdea(
        IReadOnlyList<VideoClipExtractionPlan> clipPlans,
        IReadOnlyList<string> transcriptSentences,
        GeneratedVideoAsset generatedVideoAsset)
    {
        if (clipPlans.Count > 0)
        {
            var primaryClip = clipPlans[0];
            return
                $"Use the {primaryClip.Label.ToLowerInvariant()} from {primaryClip.StartSeconds:0.##}s to {primaryClip.EndSeconds:0.##}s around '{primaryClip.TranscriptExcerpt}'.";
        }

        return $"Create an 8-12 second clip from '{transcriptSentences[0]}' and end on '{generatedVideoAsset.Title}'.";
    }

    private static IReadOnlyList<string> BuildCommentHooks(IReadOnlyList<string> transcriptSentences, string callToActionKeyword)
    {
        var hooks = transcriptSentences.Take(2).Select(ToQuestionHook).ToList();
        hooks.Add($"Want the next step? Comment {callToActionKeyword}.");
        return hooks;
    }

    private static IReadOnlyList<VideoClipExtractionPlan> BuildClipPlans(
        GeneratedVideoAsset generatedVideoAsset,
        IReadOnlyList<string> transcriptSentences)
    {
        if (generatedVideoAsset.TranscriptSegments is not { Count: > 0 })
        {
            return Array.Empty<VideoClipExtractionPlan>();
        }

        var segments = generatedVideoAsset.TranscriptSegments
            .Where(static segment => segment.EndSeconds > segment.StartSeconds && !string.IsNullOrWhiteSpace(segment.Text))
            .ToArray();
        if (segments.Length == 0)
        {
            return Array.Empty<VideoClipExtractionPlan>();
        }

        var clipPlans = new List<VideoClipExtractionPlan>();
        var introClip = BuildClipPlan("Hook Clip", segments, 0, Math.Min(2, segments.Length - 1), "hook");
        if (introClip is not null)
        {
            clipPlans.Add(introClip);
        }

        var middleIndex = Math.Clamp(segments.Length / 2, 0, segments.Length - 1);
        var insightClip = BuildClipPlan("Insight Clip", segments, Math.Max(0, middleIndex - 1), Math.Min(segments.Length - 1, middleIndex + 1), "insight");
        if (insightClip is not null)
        {
            clipPlans.Add(insightClip);
        }

        var ctaStartIndex = Math.Max(0, segments.Length - 2);
        var ctaClip = BuildClipPlan("CTA Clip", segments, ctaStartIndex, segments.Length - 1, "cta");
        if (ctaClip is not null)
        {
            clipPlans.Add(ctaClip);
        }

        return clipPlans
            .DistinctBy(static plan => plan.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static VideoClipExtractionPlan? BuildClipPlan(
        string label,
        IReadOnlyList<TimedTranscriptSegment> segments,
        int startIndex,
        int endIndex,
        string intent)
    {
        if (startIndex < 0 || endIndex < startIndex || startIndex >= segments.Count)
        {
            return null;
        }

        endIndex = Math.Min(endIndex, segments.Count - 1);
        var selectedSegments = segments.Skip(startIndex).Take(endIndex - startIndex + 1).ToArray();
        if (selectedSegments.Length == 0)
        {
            return null;
        }

        return new VideoClipExtractionPlan(
            label,
            selectedSegments[0].StartSeconds,
            selectedSegments[^1].EndSeconds,
            string.Join(" ", selectedSegments.Select(static segment => segment.Text)),
            intent);
    }

    private static string ToQuestionHook(string sentence)
    {
        var normalized = sentence.Trim().TrimEnd('.', '!', '?');
        return normalized.EndsWith("?", StringComparison.Ordinal)
            ? normalized
            : $"Have you noticed this too: {normalized}?";
    }

    [GeneratedRegex(@"(?<=[.!?])\s+|\r?\n+")]
    private static partial Regex SentenceSplitter();
}
