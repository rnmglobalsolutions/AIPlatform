using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Strategy;

namespace AIMultiAgentPlatform.Application.Strategy;

public sealed class GenerateStrategyBlueprintUseCase
{
    private const string PromptVersion = "strategy-blueprint-v1";
    private static readonly ContentCategory[] CategoryRotation =
    {
        ContentCategory.PainPoint,
        ContentCategory.Mistake,
        ContentCategory.MythBusting,
        ContentCategory.Faq,
        ContentCategory.ObjectionHandling,
        ContentCategory.Authority,
        ContentCategory.Story,
        ContentCategory.Comparison,
        ContentCategory.CtaDriven,
        ContentCategory.Urgency
    };
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ILLMStrategyPlanner? _strategyPlanner;
    private readonly IContentGuardrailValidator? _contentGuardrailValidator;

    public GenerateStrategyBlueprintUseCase(
        ILLMStrategyPlanner? strategyPlanner = null,
        IContentGuardrailValidator? contentGuardrailValidator = null)
    {
        _strategyPlanner = strategyPlanner;
        _contentGuardrailValidator = contentGuardrailValidator;
    }

    public StrategyBlueprint Execute(StrategicProfile profile, int windowDays)
    {
        var normalizedWindowDays = Math.Clamp(windowDays, 7, 28);
        var normalizedProfile = NormalizeProfile(profile);
        return BuildHeuristicBlueprint(normalizedProfile, normalizedWindowDays);
    }

    public async Task<StrategyBlueprint> ExecuteAsync(
        StrategicProfile profile,
        int windowDays,
        CancellationToken cancellationToken = default)
    {
        var normalizedWindowDays = Math.Clamp(windowDays, 7, 28);
        var normalizedProfile = NormalizeProfile(profile);
        var heuristicBlueprint = BuildHeuristicBlueprint(normalizedProfile, normalizedWindowDays);

        if (_strategyPlanner is null || _contentGuardrailValidator is null)
        {
            return heuristicBlueprint;
        }

        var plannerResult = await _strategyPlanner.GenerateAsync(
            BuildPlannerRequest(normalizedProfile, normalizedWindowDays),
            cancellationToken);

        if (!plannerResult.Succeeded)
        {
            return heuristicBlueprint;
        }

        var validationResult = await _contentGuardrailValidator.ValidateAsync(
            new ContentGuardrailValidationRequest(
                "strategy-blueprint",
                plannerResult.StrategyBlueprintJson,
                normalizedProfile.AvoidTopics),
            cancellationToken);

        if (!validationResult.IsValid)
        {
            return heuristicBlueprint;
        }

        return TryBuildLlmBlueprint(normalizedProfile, normalizedWindowDays, plannerResult.StrategyBlueprintJson, out var llmBlueprint)
            ? llmBlueprint
            : heuristicBlueprint;
    }

    private static StrategyBlueprint BuildHeuristicBlueprint(StrategicProfile profile, int normalizedWindowDays)
    {
        var planPolicy = ResolvePlanPolicy(profile.ContentPlanTier);
        return new StrategyBlueprint(
            BuildStrategicNarrative(profile),
            BuildPillars(profile),
            1,
            planPolicy.VideoCadenceDays,
            BuildBacklogBlueprint(profile, normalizedWindowDays),
            profile.ContentPlanTier,
            planPolicy.MonthlyVideoTarget);
    }

    private static bool TryBuildLlmBlueprint(
        StrategicProfile profile,
        int windowDays,
        string strategyBlueprintJson,
        out StrategyBlueprint strategyBlueprint)
    {
        strategyBlueprint = default!;

        try
        {
            var payload = JsonSerializer.Deserialize<LlmStrategyBlueprintPayload>(strategyBlueprintJson, SerializerOptions);
            if (payload is null ||
                string.IsNullOrWhiteSpace(payload.StrategicNarrative) ||
                payload.ContentPillars is null ||
                payload.BacklogBlueprintItems is null)
            {
                return false;
            }

            var mappedItems = payload.BacklogBlueprintItems
                .Select(TryMapBacklogItem)
                .ToArray();
            if (mappedItems.Length != windowDays || mappedItems.Any(static item => item is null))
            {
                return false;
            }

            var normalizedItems = mappedItems!.Cast<BacklogBlueprintItem>().ToArray();
            var expectedSequences = Enumerable.Range(1, windowDays).ToArray();
            var expectedOffsets = Enumerable.Range(0, windowDays).ToArray();
            if (!normalizedItems.Select(static item => item.Sequence).OrderBy(static value => value).SequenceEqual(expectedSequences) ||
                !normalizedItems.Select(static item => item.PlannedOffsetDays).OrderBy(static value => value).SequenceEqual(expectedOffsets))
            {
                return false;
            }

            var planPolicy = ResolvePlanPolicy(profile.ContentPlanTier);
            strategyBlueprint = new StrategyBlueprint(
                payload.StrategicNarrative.Trim(),
                payload.ContentPillars
                    .Where(static pillar => !string.IsNullOrWhiteSpace(pillar))
                    .Select(static pillar => pillar.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(6)
                    .ToArray(),
                1,
                planPolicy.VideoCadenceDays,
                normalizedItems,
                profile.ContentPlanTier,
                planPolicy.MonthlyVideoTarget);
            return strategyBlueprint.ContentPillars.Count > 0 && strategyBlueprint.BacklogBlueprintItems.Count == windowDays;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static BacklogBlueprintItem? TryMapBacklogItem(LlmBacklogBlueprintItem item)
    {
        if (!Enum.TryParse<ContentCategory>(item.Category, ignoreCase: true, out var category) ||
            !Enum.TryParse<PrimaryFormat>(item.PrimaryFormat, ignoreCase: true, out var primaryFormat) ||
            string.IsNullOrWhiteSpace(item.Topic) ||
            string.IsNullOrWhiteSpace(item.Angle) ||
            string.IsNullOrWhiteSpace(item.HookDirection) ||
            string.IsNullOrWhiteSpace(item.LeadGoal) ||
            item.Sequence <= 0 ||
            item.PlannedOffsetDays < 0)
        {
            return null;
        }

        return new BacklogBlueprintItem(
            item.Sequence,
            item.PlannedOffsetDays,
            category,
            primaryFormat,
            item.Topic.Trim(),
            item.Angle.Trim(),
            item.HookDirection.Trim(),
            item.LeadGoal.Trim(),
            item.UsesCallToActionKeyword);
    }

    private static StrategyPlannerRequest BuildPlannerRequest(StrategicProfile profile, int windowDays)
    {
        var systemContext =
            "You are a senior content strategist. Return only valid JSON that matches the requested schema. " +
            "Do not include markdown, explanations, or extra keys. Keep outputs specific to the tenant and commercially actionable.";

        var userPrompt =
            $$"""
            Build a strategy blueprint for this tenant.

            Requirements:
            - Return exactly {{windowDays}} backlog blueprint items.
            - Use sequences 1 through {{windowDays}}.
            - Use plannedOffsetDays 0 through {{windowDays - 1}}.
            - Keep the plan aligned to the stated conversion goal and CTA style.
            - Avoid generic placeholder phrasing.
            - Respect avoid-topics guidance.

            Strategic profile:
            {{JsonSerializer.Serialize(profile, SerializerOptions)}}
            """;

        return new StrategyPlannerRequest(
            profile.BusinessName,
            $"strategy-{profile.BusinessName}-{windowDays}".ToLowerInvariant(),
            PromptVersion,
            string.Empty,
            systemContext,
            userPrompt,
            new Dictionary<string, string>
            {
                ["windowDays"] = windowDays.ToString(),
                ["contentPlanTier"] = profile.ContentPlanTier.ToString()
            });
    }

    private static StrategicProfile NormalizeProfile(StrategicProfile profile)
    {
        var fallbackPainPoints = NormalizeList(profile.PainPoints, "low visibility", "inconsistent lead flow");
        var fallbackObjections = NormalizeList(profile.Objections, "no time", "uncertainty about the next step");

        return profile with
        {
            BusinessName = NormalizeValue(profile.BusinessName, "Unnamed Business"),
            Niche = NormalizeValue(profile.Niche, "General Business"),
            Offer = NormalizeValue(profile.Offer, "Growth services"),
            TargetAudience = NormalizeValue(profile.TargetAudience, "Business owners"),
            BrandTone = NormalizeValue(profile.BrandTone, "Professional"),
            ContentLanguage = NormalizeValue(profile.ContentLanguage, "English"),
            MainGoal = NormalizeValue(profile.MainGoal, "Generate more leads"),
            DesiredAction = NormalizeValue(profile.DesiredAction, "Comment or DM for more details"),
            CallToActionKeyword = NormalizeValue(profile.CallToActionKeyword, "BOOK"),
            CalendlyUrl = profile.CalendlyUrl?.Trim() ?? string.Empty,
            WebsiteUrl = profile.WebsiteUrl?.Trim() ?? string.Empty,
            PrimaryOutcome = NormalizeValue(profile.PrimaryOutcome, "generate more leads"),
            ConversionDestination = NormalizeValue(profile.ConversionDestination, "generate more leads"),
            LeadGoal = NormalizeValue(profile.LeadGoal, "generate_lead"),
            Platforms = NormalizeList(profile.Platforms, "Instagram"),
            PainPoints = fallbackPainPoints,
            Objections = fallbackObjections,
            AvoidTopics = NormalizeList(profile.AvoidTopics),
            ContentPlanTier = profile.ContentPlanTier
        };
    }

    private static (int MonthlyVideoTarget, int VideoCadenceDays) ResolvePlanPolicy(ContentPlanTier contentPlanTier) =>
        contentPlanTier switch
        {
            ContentPlanTier.Growth => (16, 2),
            ContentPlanTier.Premium => (30, 1),
            _ => (8, 4)
        };

    private static IReadOnlyList<string> BuildPillars(StrategicProfile profile)
    {
        var primaryPain = profile.PainPoints.FirstOrDefault() ?? "low visibility";
        var secondaryPain = profile.PainPoints.Skip(1).FirstOrDefault();
        var primaryObjection = profile.Objections.FirstOrDefault() ?? "uncertainty about the next step";

        var pillars = new List<string>
        {
            $"{profile.Offer} strategy for {profile.TargetAudience} in {profile.Niche}",
            $"How {profile.TargetAudience} can solve {NormalizeTopicFragment(primaryPain)}",
            $"How {profile.TargetAudience} can overcome {NormalizeTopicFragment(primaryObjection)}",
            $"Proof, use cases, and client outcomes around {profile.Offer}",
            BuildConversionPillar(profile)
        };

        if (!string.IsNullOrWhiteSpace(secondaryPain))
        {
            pillars.Insert(2, $"How {profile.TargetAudience} can solve {NormalizeTopicFragment(secondaryPain)}");
        }

        return pillars
            .Select(static pillar => pillar.Trim())
            .Where(static pillar => !string.IsNullOrWhiteSpace(pillar))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
    }

    private static string BuildStrategicNarrative(StrategicProfile profile)
    {
        var primaryPain = NormalizeTopicFragment(profile.PainPoints.FirstOrDefault() ?? "low visibility");
        var primaryObjection = NormalizeTopicFragment(profile.Objections.FirstOrDefault() ?? "uncertainty about the next step");
        var audience = profile.TargetAudience.ToLowerInvariant();
        var offer = profile.Offer.ToLowerInvariant();

        return
            $"{profile.BusinessName} should publish daily {profile.ContentLanguage.ToLowerInvariant()} content for {audience} that positions {offer} as the trusted answer to {primaryPain}, dismantles objections like {primaryObjection}, and consistently moves the audience toward {profile.ConversionDestination}.";
    }

    private static string BuildConversionPillar(StrategicProfile profile) =>
        profile.ConversionMode switch
        {
            StrategyConversionMode.Booking => $"Conversion content that moves {profile.TargetAudience} toward booked consultations",
            StrategyConversionMode.Website => $"Conversion content that moves {profile.TargetAudience} toward high-intent website visits",
            StrategyConversionMode.DirectMessage => $"Conversion content that moves {profile.TargetAudience} into direct-message conversations",
            StrategyConversionMode.CommentKeyword => $"Conversion content that moves {profile.TargetAudience} into comment-driven lead capture",
            _ => $"Conversion content aligned to {NormalizeTopicFragment(profile.PrimaryOutcome)}"
        };

    private static IReadOnlyList<BacklogBlueprintItem> BuildBacklogBlueprint(StrategicProfile profile, int windowDays)
    {
        var items = new List<BacklogBlueprintItem>(windowDays);
        for (var day = 0; day < windowDays; day++)
        {
            var category = CategoryRotation[day % CategoryRotation.Length];
            var primaryFormat = (day + 1) % 3 == 0 ? PrimaryFormat.ShortVideo : PrimaryFormat.BrandedGraphic;
            var anchor = ResolveAnchor(profile, category, day);
            items.Add(
                new BacklogBlueprintItem(
                    day + 1,
                    day,
                    category,
                    primaryFormat,
                    anchor.Topic,
                    anchor.Angle,
                    anchor.HookDirection,
                    profile.LeadGoal,
                    UsesKeywordDrivenCallToAction(profile, category)));
        }

        return items;
    }

    private static (string Topic, string Angle, string HookDirection) ResolveAnchor(StrategicProfile profile, ContentCategory category, int index)
    {
        var painPoint = profile.PainPoints[index % profile.PainPoints.Count];
        var objection = profile.Objections[index % profile.Objections.Count];
        var normalizedPain = NormalizeTopicFragment(painPoint);
        var normalizedObjection = NormalizeTopicFragment(objection);
        var normalizedGoal = NormalizeTopicFragment(profile.PrimaryOutcome);

        return category switch
        {
            ContentCategory.PainPoint => (
                $"Why {normalizedPain} keeps {profile.TargetAudience} from {normalizedGoal}",
                $"Expose the business cost of leaving {normalizedPain} unresolved and show the first practical shift toward {profile.Offer}",
                "Open by naming the pain the audience already feels"),
            ContentCategory.Mistake => (
                $"Mistakes {profile.TargetAudience} make when trying to {normalizedGoal}",
                $"Show the avoidable mistake that makes {normalizedGoal} harder and reframe the better path through {profile.Offer}",
                "Lead with the most expensive mistake first"),
            ContentCategory.MythBusting => (
                $"Myths about {profile.Offer} that keep {profile.TargetAudience} stuck",
                $"Challenge the false belief that blocks progress toward {normalizedGoal}",
                "Break a widely believed myth in the opening line"),
            ContentCategory.Faq => (
                $"What buyers ask before investing in {profile.Offer}",
                $"Answer a high-friction question in a simple, confidence-building way so the audience can move toward {profile.ConversionDestination}",
                "Use a question-based hook that sounds like the buyer's inner dialogue"),
            ContentCategory.ObjectionHandling => (
                $"How to handle '{normalizedObjection}' before choosing {profile.Offer}",
                $"Reframe the objection without pressure and show what the audience gains by moving forward",
                "Address the objection directly and empathetically"),
            ContentCategory.Authority => (
                $"{profile.Niche} insights that improve {normalizedGoal}",
                $"Teach a nuanced insight that positions {profile.BusinessName} as the trusted guide for {profile.TargetAudience}",
                "Reveal the expert perspective most buyers miss"),
            ContentCategory.Story => (
                $"A client-style story about moving from {normalizedPain} to {normalizedGoal}",
                $"Use a believable before-and-after scenario that makes the transformation feel attainable through {profile.Offer}",
                "Open with a vivid scenario the audience can see themselves in"),
            ContentCategory.Comparison => (
                $"{profile.Offer} versus patchwork solutions for {profile.TargetAudience}",
                $"Contrast the reactive path with the more strategic path and make the tradeoff clear",
                "Use a side-by-side contrast hook"),
            ContentCategory.CtaDriven => (
                BuildCallToActionTopic(profile),
                BuildCallToActionAngle(profile),
                BuildCallToActionHook(profile)),
            _ => (
                $"What delaying {profile.Offer} costs {profile.TargetAudience}",
                $"Create urgency by showing how waiting makes {normalizedPain} and {normalizedGoal} harder to improve",
                "Lead with the cost of waiting, not generic urgency")
        };
    }

    private static bool UsesKeywordDrivenCallToAction(StrategicProfile profile, ContentCategory category)
    {
        if (profile.LeadGoal is not ("send_dm" or "comment_keyword"))
        {
            return false;
        }

        return category is ContentCategory.CtaDriven or ContentCategory.ObjectionHandling or ContentCategory.Urgency;
    }

    private static string BuildCallToActionTopic(StrategicProfile profile) =>
        profile.ConversionMode switch
        {
            StrategyConversionMode.Booking => $"What to expect before booking {profile.Offer}",
            StrategyConversionMode.Website => $"What {profile.TargetAudience} should review before visiting the website",
            StrategyConversionMode.DirectMessage => $"When to send a DM about {profile.Offer}",
            StrategyConversionMode.CommentKeyword => $"When to comment '{profile.CallToActionKeyword}' for the next step",
            _ => $"How to take the next step toward {NormalizeTopicFragment(profile.PrimaryOutcome)}"
        };

    private static string BuildCallToActionAngle(StrategicProfile profile) =>
        profile.ConversionMode switch
        {
            StrategyConversionMode.Booking => $"Reduce booking friction by showing the audience exactly why and when to schedule with {profile.BusinessName}",
            StrategyConversionMode.Website => "Build enough curiosity and trust to send the audience to the website with clear purchase intent",
            StrategyConversionMode.DirectMessage => "Lower the barrier to conversation and make the DM feel like the natural next step",
            StrategyConversionMode.CommentKeyword => $"Create curiosity around the keyword {profile.CallToActionKeyword} without sounding gimmicky",
            _ => $"Move the audience toward {NormalizeTopicFragment(profile.PrimaryOutcome)} with a clear next step"
        };

    private static string BuildCallToActionHook(StrategicProfile profile) =>
        profile.ConversionMode switch
        {
            StrategyConversionMode.Booking => "Open by removing the fear or uncertainty behind booking",
            StrategyConversionMode.Website => "Open with a curiosity gap that makes the website visit feel valuable",
            StrategyConversionMode.DirectMessage => "Open by making the conversation feel easy and low-pressure",
            StrategyConversionMode.CommentKeyword => "Open with a CTA-oriented curiosity gap",
            _ => "Open with the clearest next step available"
        };

    private static string NormalizeTopicFragment(string value) =>
        value.Trim().TrimEnd('.', '!', '?').ToLowerInvariant();

    private static string NormalizeValue(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values, params string[] fallback) =>
        values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() is { Length: > 0 } normalized
                ? normalized
                : fallback
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

    private sealed record LlmStrategyBlueprintPayload(
        string StrategicNarrative,
        IReadOnlyList<string> ContentPillars,
        IReadOnlyList<LlmBacklogBlueprintItem> BacklogBlueprintItems);

    private sealed record LlmBacklogBlueprintItem(
        int Sequence,
        int PlannedOffsetDays,
        string Category,
        string PrimaryFormat,
        string Topic,
        string Angle,
        string HookDirection,
        string LeadGoal,
        bool UsesCallToActionKeyword);
}
