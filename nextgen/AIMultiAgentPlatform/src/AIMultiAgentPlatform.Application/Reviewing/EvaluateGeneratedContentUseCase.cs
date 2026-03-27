using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Reviewing;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Reviewing;

public sealed class EvaluateGeneratedContentUseCase
{
    private static readonly string[] GenericPhrases =
    [
        "game changer",
        "next level",
        "supercharge",
        "everyone",
        "anyone",
        "thing",
        "stuff"
    ];

    public GeneratedContentEvaluation Execute(
        ClientProfile profile,
        DailyContentBrief brief,
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        RepurposedAssetBundle repurposedAssetBundle)
    {
        var warnings = new List<string>();
        var desiredActionAligned = IsDesiredActionAligned(profile, primaryAsset, captionAsset, repurposedAssetBundle);
        var languageAligned = IsLanguageAligned(profile, primaryAsset, captionAsset);
        var ctaKeywordPresent = CaptionIncludesKeyword(captionAsset, brief.CallToActionKeyword) ||
                                repurposedAssetBundle.CommentHooks.Any(hook => hook.Contains(brief.CallToActionKeyword, StringComparison.OrdinalIgnoreCase));

        var hookScore = EvaluateHook(primaryAsset, warnings);
        var clarityScore = EvaluateClarity(primaryAsset, warnings);
        var relevanceScore = EvaluateRelevance(profile, brief, primaryAsset, warnings);
        var leadGenerationScore = EvaluateLeadGeneration(desiredActionAligned, languageAligned, ctaKeywordPresent, repurposedAssetBundle, warnings);
        var specificityScore = EvaluateSpecificity(profile, brief, primaryAsset, captionAsset, warnings);
        var platformFitScore = EvaluatePlatformFit(primaryAsset, captionAsset, repurposedAssetBundle, warnings);
        var antiRepetitionScore = EvaluateAntiRepetition(primaryAsset, captionAsset, repurposedAssetBundle, warnings);

        var overall = Math.Round(
            new[]
            {
                hookScore,
                clarityScore,
                relevanceScore,
                leadGenerationScore,
                specificityScore,
                platformFitScore,
                antiRepetitionScore
            }.Average(),
            2);

        var strongestArea = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["hook strength"] = hookScore,
            ["clarity"] = clarityScore,
            ["relevance"] = relevanceScore,
            ["lead generation"] = leadGenerationScore,
            ["specificity"] = specificityScore,
            ["platform fit"] = platformFitScore,
            ["anti-repetition"] = antiRepetitionScore
        }
        .OrderByDescending(static item => item.Value)
        .First().Key;

        var feedback =
            warnings.Count == 0
                ? $"Strongest area: {strongestArea}. Keep the message aligned to the desired action '{profile.DesiredAction}' and preserve this level of specificity for {profile.TargetAudience.ToLowerInvariant()}."
                : $"Strongest area: {strongestArea}. Tighten the package by addressing: {string.Join("; ", warnings.Take(3))}. Keep the content aligned to the desired action '{profile.DesiredAction}'.";

        return new GeneratedContentEvaluation(
            hookScore,
            clarityScore,
            relevanceScore,
            leadGenerationScore,
            specificityScore,
            platformFitScore,
            antiRepetitionScore,
            overall,
            feedback,
            BuildOptimizedCallToAction(profile, brief.CallToActionKeyword),
            warnings);
    }

    private static double EvaluateHook(PrimaryAsset primaryAsset, IList<string> warnings)
    {
        var hook = primaryAsset.Hook.Trim();
        var score = hook.Length is >= 24 and <= 140 ? 8.9 : 7.0;

        if (ContainsGenericPhrase(hook))
        {
            score -= 1.1;
            warnings.Add("Hook feels generic and could use a sharper opening line.");
        }

        if (hook.Equals(primaryAsset.Headline, StringComparison.OrdinalIgnoreCase))
        {
            score -= 0.8;
            warnings.Add("Hook is too close to the headline and loses stopping power.");
        }

        return ClampScore(score);
    }

    private static double EvaluateClarity(PrimaryAsset primaryAsset, IList<string> warnings)
    {
        var body = primaryAsset.Body;
        var score = body.Contains("BODY:", StringComparison.Ordinal) || body.Contains("Lead with", StringComparison.OrdinalIgnoreCase)
            ? 8.8
            : 7.1;

        if (body.Length > 900)
        {
            score -= 0.8;
            warnings.Add("Primary asset body is long and may reduce clarity.");
        }

        return ClampScore(score);
    }

    private static double EvaluateRelevance(ClientProfile profile, DailyContentBrief brief, PrimaryAsset primaryAsset, IList<string> warnings)
    {
        var corpus = string.Join("\n", brief.CoreMessage, primaryAsset.Body, primaryAsset.Payoff);
        var score = 7.2;

        if (corpus.Contains(profile.Offer, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.9;
        }

        if (corpus.Contains(profile.TargetAudience, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.9;
        }

        if (corpus.Contains(brief.Topic, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.8;
        }

        if (score < 8.0)
        {
            warnings.Add("Content relevance could be tighter to the offer, audience, or topic.");
        }

        return ClampScore(score);
    }

    private static double EvaluateLeadGeneration(
        bool desiredActionAligned,
        bool languageAligned,
        bool ctaKeywordPresent,
        RepurposedAssetBundle repurposedAssetBundle,
        IList<string> warnings)
    {
        var score = desiredActionAligned ? 8.8 : 6.9;

        if (!languageAligned)
        {
            score -= 0.7;
            warnings.Add("Language execution is not fully aligned to the tenant preference.");
        }

        if (!ctaKeywordPresent)
        {
            score -= 1.0;
            warnings.Add("CTA keyword is missing from the caption or follow-up hooks.");
        }

        if (repurposedAssetBundle.CommentHooks.Count < 2)
        {
            score -= 0.8;
            warnings.Add("Repurposed comment hooks are too thin for lead capture.");
        }

        return ClampScore(score);
    }

    private static double EvaluateSpecificity(
        ClientProfile profile,
        DailyContentBrief brief,
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        IList<string> warnings)
    {
        var corpus = string.Join("\n", brief.CoreMessage, primaryAsset.Body, captionAsset.Caption);
        var genericMatches = GenericPhrases.Count(fragment => corpus.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        var score = 9.0 - (genericMatches * 0.5);

        if (!corpus.Contains(profile.TargetAudience, StringComparison.OrdinalIgnoreCase))
        {
            score -= 0.7;
        }

        if (!corpus.Contains(profile.Offer, StringComparison.OrdinalIgnoreCase) &&
            !corpus.Contains(profile.Niche, StringComparison.OrdinalIgnoreCase))
        {
            score -= 0.7;
        }

        if (score < 7.8)
        {
            warnings.Add("Content feels too generic and could be more tenant-specific.");
        }

        return ClampScore(score);
    }

    private static double EvaluatePlatformFit(
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        RepurposedAssetBundle repurposedAssetBundle,
        IList<string> warnings)
    {
        var score = primaryAsset.PrimaryFormat switch
        {
            Domain.Editorial.PrimaryFormat.ShortVideo when primaryAsset.ProductionNotes.Contains("HeyGen", StringComparison.OrdinalIgnoreCase) => 8.9,
            Domain.Editorial.PrimaryFormat.BrandedGraphic when primaryAsset.ProductionNotes.Contains("Canva", StringComparison.OrdinalIgnoreCase) => 8.9,
            _ => 7.1
        };

        if (captionAsset.Caption.Length > 650)
        {
            score -= 0.8;
            warnings.Add("Caption may be too long for fast-moving social formats.");
        }

        if (repurposedAssetBundle.StoryFrames.Count < 3)
        {
            score -= 0.5;
            warnings.Add("Story repurposing is thinner than expected.");
        }

        return ClampScore(score);
    }

    private static double EvaluateAntiRepetition(
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        RepurposedAssetBundle repurposedAssetBundle,
        IList<string> warnings)
    {
        var commentHookDistinctCount = repurposedAssetBundle.CommentHooks
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var storyFrameDistinctCount = repurposedAssetBundle.StoryFrames
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var score = 8.8;
        if (commentHookDistinctCount != repurposedAssetBundle.CommentHooks.Count)
        {
            score -= 0.9;
            warnings.Add("Comment hooks are repetitive and need more variety.");
        }

        if (storyFrameDistinctCount != repurposedAssetBundle.StoryFrames.Count)
        {
            score -= 0.8;
            warnings.Add("Story frames are repetitive and could feel templated.");
        }

        if (captionAsset.Caption.Contains(primaryAsset.Hook, StringComparison.OrdinalIgnoreCase) &&
            captionAsset.Caption.Count(character => character == '.') < 2)
        {
            score -= 0.5;
            warnings.Add("Caption leans too heavily on the hook without enough development.");
        }

        return ClampScore(score);
    }

    private static bool ContainsGenericPhrase(string value) =>
        GenericPhrases.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool CaptionIncludesKeyword(CaptionAsset captionAsset, string keyword) =>
        !string.IsNullOrWhiteSpace(keyword) &&
        captionAsset.Caption.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static double ClampScore(double score) => Math.Round(Math.Clamp(score, 5.5, 9.5), 2);

    private static bool IsDesiredActionAligned(
        ClientProfile profile,
        PrimaryAsset primaryAsset,
        CaptionAsset captionAsset,
        RepurposedAssetBundle repurposedAssetBundle)
    {
        var corpus = string.Join(
            "\n",
            profile.DesiredAction,
            primaryAsset.CallToAction,
            captionAsset.Caption,
            repurposedAssetBundle.CarouselOutline,
            repurposedAssetBundle.LinkedInPost,
            string.Join("\n", repurposedAssetBundle.CommentHooks));

        return string.IsNullOrWhiteSpace(profile.DesiredAction) ||
               corpus.Contains("book", StringComparison.OrdinalIgnoreCase) && profile.DesiredAction.Contains("book", StringComparison.OrdinalIgnoreCase) ||
               corpus.Contains("comment", StringComparison.OrdinalIgnoreCase) && profile.DesiredAction.Contains("comment", StringComparison.OrdinalIgnoreCase) ||
               corpus.Contains("dm", StringComparison.OrdinalIgnoreCase) && profile.DesiredAction.Contains("dm", StringComparison.OrdinalIgnoreCase) ||
               corpus.Contains("website", StringComparison.OrdinalIgnoreCase) && profile.DesiredAction.Contains("website", StringComparison.OrdinalIgnoreCase) ||
               corpus.Contains("visit", StringComparison.OrdinalIgnoreCase) && profile.DesiredAction.Contains("visit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLanguageAligned(ClientProfile profile, PrimaryAsset primaryAsset, CaptionAsset captionAsset)
    {
        if (string.Equals(profile.ContentLanguage, "English", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var corpus = string.Join("\n", primaryAsset.ProductionNotes, captionAsset.Caption);
        return profile.ContentLanguage switch
        {
            "Spanish" => corpus.Contains("Spanish", StringComparison.OrdinalIgnoreCase),
            "Bilingual" => corpus.Contains("Spanish", StringComparison.OrdinalIgnoreCase) &&
                           corpus.Contains("English", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static string BuildOptimizedCallToAction(ClientProfile profile, string keyword)
    {
        if (!string.IsNullOrWhiteSpace(profile.CalendlyUrl) &&
            profile.DesiredAction.Contains("book", StringComparison.OrdinalIgnoreCase))
        {
            return $"Book via {profile.CalendlyUrl} or DM '{keyword}' if you want help first.";
        }

        if (!string.IsNullOrWhiteSpace(profile.WebsiteUrl) &&
            profile.DesiredAction.Contains("website", StringComparison.OrdinalIgnoreCase))
        {
            return $"Visit {profile.WebsiteUrl} and use '{keyword}' if you want help choosing the right next step.";
        }

        return $"Use '{keyword}' in the CTA so the audience knows exactly how to respond.";
    }
}
