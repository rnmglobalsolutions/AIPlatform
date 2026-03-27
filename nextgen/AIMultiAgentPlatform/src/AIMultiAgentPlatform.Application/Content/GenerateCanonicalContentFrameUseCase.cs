using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Content;

public sealed class GenerateCanonicalContentFrameUseCase
{
    private const string PromptVersion = "canonical-content-frame-v1";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ILLMContentGenerator? _contentGenerator;
    private readonly IContentGuardrailValidator? _contentGuardrailValidator;

    public GenerateCanonicalContentFrameUseCase(
        ILLMContentGenerator? contentGenerator = null,
        IContentGuardrailValidator? contentGuardrailValidator = null)
    {
        _contentGenerator = contentGenerator;
        _contentGuardrailValidator = contentGuardrailValidator;
    }

    public CanonicalContentFrame Execute(
        TenantId tenantId,
        ClientProfile profile,
        EditorialBacklogItem backlogItem,
        ContentMemorySnapshot? contentMemorySnapshot = null)
    {
        return BuildHeuristicFrame(tenantId, profile, backlogItem, contentMemorySnapshot);
    }

    public async Task<CanonicalContentFrame> ExecuteAsync(
        TenantId tenantId,
        ClientProfile profile,
        EditorialBacklogItem backlogItem,
        ContentMemorySnapshot? contentMemorySnapshot = null,
        CancellationToken cancellationToken = default)
    {
        var heuristicFrame = BuildHeuristicFrame(tenantId, profile, backlogItem, contentMemorySnapshot);
        if (_contentGenerator is null || _contentGuardrailValidator is null)
        {
            return heuristicFrame;
        }

        var contentResult = await _contentGenerator.GenerateAsync(
            BuildContentGenerationRequest(tenantId, profile, backlogItem, heuristicFrame),
            cancellationToken);

        if (!contentResult.Succeeded)
        {
            return heuristicFrame;
        }

        var normalizedProfile = profile.Normalize();
        var validationResult = await _contentGuardrailValidator.ValidateAsync(
            new ContentGuardrailValidationRequest(
                "canonical-content-frame",
                contentResult.GeneratedPayloadJson,
                normalizedProfile.AvoidTopics),
            cancellationToken);

        if (!validationResult.IsValid)
        {
            return heuristicFrame;
        }

        return TryBuildLlmFrame(tenantId, normalizedProfile, backlogItem, heuristicFrame, contentResult.GeneratedPayloadJson, out var llmFrame)
            ? llmFrame
            : heuristicFrame;
    }

    private static CanonicalContentFrame BuildHeuristicFrame(
        TenantId tenantId,
        ClientProfile profile,
        EditorialBacklogItem backlogItem,
        ContentMemorySnapshot? contentMemorySnapshot)
    {
        var normalizedProfile = profile.Normalize();
        var languageGuidance = BuildLanguageGuidance(normalizedProfile);
        var languageFormatInstruction = BuildLanguageFormatInstruction(normalizedProfile);
        var recentTopicsToAvoid = ResolveRecentTopicsToAvoid(contentMemorySnapshot, backlogItem.Topic);
        var recentHooksToAvoid = ResolveRecentHooksToAvoid(contentMemorySnapshot, backlogItem.HookDirection);
        var antiRepetitionGuidance = BuildAntiRepetitionGuidance(recentTopicsToAvoid, recentHooksToAvoid);

        var coreMessage =
            $"{backlogItem.Angle}. Show {normalizedProfile.TargetAudience.ToLowerInvariant()} how {normalizedProfile.Offer.ToLowerInvariant()} helps them move past {backlogItem.Topic.ToLowerInvariant()} without sounding repetitive. {languageGuidance} Keep it aligned to the goal of {normalizedProfile.MainGoal.ToLowerInvariant()} and the desired action of {normalizedProfile.DesiredAction.ToLowerInvariant()}.{antiRepetitionGuidance}";

        var primaryHook = $"{backlogItem.HookDirection}. {backlogItem.Topic}.";
        var body = backlogItem.PrimaryFormat == PrimaryFormat.ShortVideo
            ? $"HOOK: {primaryHook}\nBODY: Teach one practical shift around {backlogItem.Angle.ToLowerInvariant()} for {normalizedProfile.TargetAudience.ToLowerInvariant()}. {languageFormatInstruction}\nPAYOFF: Tie the lesson back to {normalizedProfile.Offer.ToLowerInvariant()} with a clear next step that supports {normalizedProfile.MainGoal.ToLowerInvariant()}."
            : $"Lead with a bold headline about {backlogItem.Topic}. Reinforce {backlogItem.Angle.ToLowerInvariant()} in concise supporting copy for a Canva-ready branded graphic. {languageFormatInstruction}";
        var payoff = backlogItem.PrimaryFormat == PrimaryFormat.ShortVideo
            ? $"Leave the audience with one simple action they can take today to improve {normalizedProfile.Niche.ToLowerInvariant()} performance and move closer to {normalizedProfile.MainGoal.ToLowerInvariant()}."
            : $"Make the visual feel actionable and save-worthy so the audience wants to revisit the message later and feel ready to {normalizedProfile.DesiredAction.ToLowerInvariant()}.";
        var callToAction = BuildCallToAction(normalizedProfile, normalizedProfile.CallToActionKeyword);
        var engagementPrompt = BuildEngagementPrompt(normalizedProfile, backlogItem.Topic);
        var desiredActionPrompt = BuildDesiredActionPrompt(normalizedProfile, normalizedProfile.CallToActionKeyword);
        var productionNotes = backlogItem.PrimaryFormat == PrimaryFormat.ShortVideo
            ? $"15-45 second HeyGen-compatible script. {languageFormatInstruction} Keep cadence natural, conversational, and easy to subtitle in {normalizedProfile.ContentLanguage.ToLowerInvariant()}."
            : $"Design in Canva with strong brand hierarchy, one core message, and a CTA-ready footer treatment. {languageFormatInstruction}";

        return new CanonicalContentFrame(
            tenantId,
            backlogItem.Category,
            backlogItem.PrimaryFormat,
            backlogItem.Topic,
            backlogItem.Angle,
            backlogItem.HookDirection,
            primaryHook,
            BuildHookVariants(backlogItem),
            coreMessage,
            body,
            payoff,
            callToAction,
            engagementPrompt,
            desiredActionPrompt,
            normalizedProfile.CallToActionKeyword,
            languageGuidance,
            languageFormatInstruction,
            productionNotes,
            BuildRepurposeDirectives(normalizedProfile, backlogItem, normalizedProfile.CallToActionKeyword),
            recentTopicsToAvoid,
            recentHooksToAvoid);
    }

    private static bool TryBuildLlmFrame(
        TenantId tenantId,
        ClientProfile profile,
        EditorialBacklogItem backlogItem,
        CanonicalContentFrame heuristicFrame,
        string generatedPayloadJson,
        out CanonicalContentFrame canonicalContentFrame)
    {
        canonicalContentFrame = default!;

        try
        {
            var payload = JsonSerializer.Deserialize<LlmCanonicalContentFramePayload>(generatedPayloadJson, SerializerOptions);
            if (payload is null ||
                string.IsNullOrWhiteSpace(payload.PrimaryHook) ||
                string.IsNullOrWhiteSpace(payload.CoreMessage) ||
                string.IsNullOrWhiteSpace(payload.Body) ||
                string.IsNullOrWhiteSpace(payload.Payoff) ||
                string.IsNullOrWhiteSpace(payload.CallToAction) ||
                string.IsNullOrWhiteSpace(payload.EngagementPrompt) ||
                string.IsNullOrWhiteSpace(payload.DesiredActionPrompt) ||
                string.IsNullOrWhiteSpace(payload.LanguageGuidance) ||
                string.IsNullOrWhiteSpace(payload.LanguageFormatInstruction) ||
                string.IsNullOrWhiteSpace(payload.ProductionNotes) ||
                payload.HookVariants is null ||
                payload.RepurposeDirectives is null)
            {
                return false;
            }

            var hookVariants = payload.HookVariants
                .Where(static item => !string.IsNullOrWhiteSpace(item.Label) && !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => new HookVariant(item.Label.Trim(), item.Text.Trim()))
                .DistinctBy(static item => item.Label, StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToArray();
            var repurposeDirectives = payload.RepurposeDirectives
                .Where(static item =>
                    !string.IsNullOrWhiteSpace(item.Format) &&
                    !string.IsNullOrWhiteSpace(item.Intent) &&
                    !string.IsNullOrWhiteSpace(item.Prompt))
                .Select(item => new RepurposeDirective(item.Format.Trim(), item.Intent.Trim(), item.Prompt.Trim()))
                .DistinctBy(static item => item.Format, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();

            if (hookVariants.Length == 0 || repurposeDirectives.Length == 0)
            {
                return false;
            }

            canonicalContentFrame = new CanonicalContentFrame(
                tenantId,
                backlogItem.Category,
                backlogItem.PrimaryFormat,
                backlogItem.Topic,
                backlogItem.Angle,
                backlogItem.HookDirection,
                payload.PrimaryHook.Trim(),
                hookVariants,
                payload.CoreMessage.Trim(),
                payload.Body.Trim(),
                payload.Payoff.Trim(),
                payload.CallToAction.Trim(),
                payload.EngagementPrompt.Trim(),
                payload.DesiredActionPrompt.Trim(),
                profile.CallToActionKeyword,
                payload.LanguageGuidance.Trim(),
                payload.LanguageFormatInstruction.Trim(),
                payload.ProductionNotes.Trim(),
                repurposeDirectives,
                heuristicFrame.RecentTopicsToAvoid,
                heuristicFrame.RecentHooksToAvoid);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ContentGenerationRequest BuildContentGenerationRequest(
        TenantId tenantId,
        ClientProfile profile,
        EditorialBacklogItem backlogItem,
        CanonicalContentFrame heuristicFrame)
    {
        var systemContext =
            "You are a senior content strategist and copywriter. Return only valid JSON matching the requested schema. " +
            "Keep outputs specific, platform-aware, non-generic, and commercially useful.";

        var promptContext = new
        {
            TenantId = tenantId.Value,
            Profile = profile.Normalize(),
            BacklogItem = backlogItem,
            BaselineFrame = heuristicFrame,
            Constraints = new
            {
                PreserveTopic = backlogItem.Topic,
                PreserveAngle = backlogItem.Angle,
                PreserveHookDirection = backlogItem.HookDirection,
                PreservePrimaryFormat = backlogItem.PrimaryFormat.ToString(),
                PreserveCallToActionKeyword = profile.Normalize().CallToActionKeyword
            }
        };

        var userPrompt =
            """
            Generate a stronger canonical content frame for this backlog item.

            Requirements:
            - Keep the same topic, angle, hook direction, and primary format.
            - Improve specificity, novelty, and spoken/written quality.
            - Respect language, tone, CTA mode, avoid-topics guidance, and anti-repetition context.
            - Make hook variants meaningfully different from each other.
            - Make repurpose directives platform-aware and immediately usable.
            - Return only JSON.

            Context:
            """ + "\n" + JsonSerializer.Serialize(promptContext, SerializerOptions);

        return new ContentGenerationRequest(
            tenantId.Value,
            $"content-{tenantId.Value}-{backlogItem.Sequence}",
            PromptVersion,
            string.Empty,
            systemContext,
            userPrompt,
            new Dictionary<string, string>
            {
                ["primaryFormat"] = backlogItem.PrimaryFormat.ToString(),
                ["category"] = backlogItem.Category.ToString(),
                ["sequence"] = backlogItem.Sequence.ToString()
            });
    }

    private static IReadOnlyList<HookVariant> BuildHookVariants(EditorialBacklogItem backlogItem) =>
        new[]
        {
            new HookVariant("primary", $"{backlogItem.HookDirection}. {backlogItem.Topic}."),
            new HookVariant("contrarian", $"Most people get {backlogItem.Topic.ToLowerInvariant()} wrong. Here's the better move."),
            new HookVariant("question", $"What if {backlogItem.Topic.ToLowerInvariant()} is the real reason results feel stuck?")
        };

    private static IReadOnlyList<RepurposeDirective> BuildRepurposeDirectives(
        ClientProfile profile,
        EditorialBacklogItem backlogItem,
        string callToActionKeyword) =>
        new[]
        {
            new RepurposeDirective("Carousel", "Teach the angle slide by slide", $"Close with {BuildRepurposeCallToAction(profile, callToActionKeyword)}."),
            new RepurposeDirective("Stories", "Break the insight into three frames", $"Use a CTA sticker that points to {BuildRepurposeCallToAction(profile, callToActionKeyword)}."),
            new RepurposeDirective("LinkedIn", "Expand the topic into a professional point of view", $"End with {BuildDesiredActionPrompt(profile, callToActionKeyword)}.")
        };

    private static IReadOnlyList<string> ResolveRecentTopicsToAvoid(ContentMemorySnapshot? contentMemorySnapshot, string currentTopic) =>
        ResolvePreferredTopics(contentMemorySnapshot)
            .Where(topic => !topic.Equals(currentTopic, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToArray();

    private static IReadOnlyList<string> ResolveRecentHooksToAvoid(ContentMemorySnapshot? contentMemorySnapshot, string currentHookDirection) =>
        ResolvePreferredHooks(contentMemorySnapshot)
            .Where(hook => !hook.Equals(currentHookDirection, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToArray();

    private static IReadOnlyList<string> ResolvePreferredTopics(ContentMemorySnapshot? contentMemorySnapshot)
    {
        if (contentMemorySnapshot is null)
        {
            return Array.Empty<string>();
        }

        return contentMemorySnapshot.RecentPublishedTopics.Count > 0
            ? contentMemorySnapshot.RecentPublishedTopics
            : contentMemorySnapshot.RecentTopics;
    }

    private static IReadOnlyList<string> ResolvePreferredHooks(ContentMemorySnapshot? contentMemorySnapshot)
    {
        if (contentMemorySnapshot is null)
        {
            return Array.Empty<string>();
        }

        return contentMemorySnapshot.RecentPublishedHooks.Count > 0
            ? contentMemorySnapshot.RecentPublishedHooks
            : contentMemorySnapshot.RecentHooks;
    }

    private static string BuildAntiRepetitionGuidance(
        IReadOnlyList<string> recentTopicsToAvoid,
        IReadOnlyList<string> recentHooksToAvoid)
    {
        var parts = new List<string>(2);

        if (recentTopicsToAvoid.Count > 0)
        {
            parts.Add($"Avoid repeating recent topics like {string.Join(", ", recentTopicsToAvoid.Select(static topic => topic.ToLowerInvariant()))}.");
        }

        if (recentHooksToAvoid.Count > 0)
        {
            parts.Add($"Avoid reusing hook patterns like {string.Join(", ", recentHooksToAvoid.Select(static hook => hook.ToLowerInvariant()))}.");
        }

        return parts.Count == 0 ? string.Empty : $" {string.Join(" ", parts)}";
    }

    private static string BuildLanguageGuidance(ClientProfile profile) =>
        profile.ContentLanguage switch
        {
            "Spanish" => "Write the content in Spanish.",
            "Bilingual" => "Deliver the content in bilingual format with English first and Spanish immediately after when practical.",
            _ => "Write the content in English."
        };

    private static string BuildLanguageFormatInstruction(ClientProfile profile) =>
        profile.ContentLanguage switch
        {
            "Spanish" => "Use Spanish-first phrasing.",
            "Bilingual" => "Format key lines in both English and Spanish.",
            _ => "Use English-first phrasing."
        };

    private static string BuildCallToAction(ClientProfile profile, string callToActionKeyword)
    {
        if (RequiresBookingCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.CalendlyUrl))
        {
            return $"Invite the audience to book directly through {profile.CalendlyUrl} or DM '{callToActionKeyword}' if they want help first.";
        }

        if (RequiresWebsiteCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.WebsiteUrl))
        {
            return $"Invite the audience to visit {profile.WebsiteUrl} and mention '{callToActionKeyword}' if they want guidance before taking the next step.";
        }

        if (ContainsAny(profile.DesiredAction, "comment"))
        {
            return $"Invite the audience to comment '{callToActionKeyword}'.";
        }

        if (ContainsAny(profile.DesiredAction, "dm", "message"))
        {
            return $"Invite the audience to DM '{callToActionKeyword}'.";
        }

        if (RequiresBookingCallToAction(profile))
        {
            return $"Invite the audience to book a consultation and mention '{callToActionKeyword}' when they reach out.";
        }

        return $"Invite the audience to {profile.DesiredAction.ToLowerInvariant()} and use '{callToActionKeyword}' as the conversion keyword.";
    }

    private static string BuildDesiredActionPrompt(ClientProfile profile, string callToActionKeyword)
    {
        if (RequiresBookingCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.CalendlyUrl))
        {
            return $"Book through {profile.CalendlyUrl} or DM '{callToActionKeyword}' if you want the right next step.";
        }

        if (RequiresWebsiteCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.WebsiteUrl))
        {
            return $"Visit {profile.WebsiteUrl} and use '{callToActionKeyword}' if you want help choosing the right option.";
        }

        if (ContainsAny(profile.DesiredAction, "comment"))
        {
            return $"Comment '{callToActionKeyword}' to keep the conversation going.";
        }

        if (ContainsAny(profile.DesiredAction, "dm", "message"))
        {
            return $"DM '{callToActionKeyword}' to keep the conversation going.";
        }

        if (RequiresBookingCallToAction(profile))
        {
            return $"Book a consultation or message '{callToActionKeyword}' if you want support.";
        }

        return $"{profile.DesiredAction.TrimEnd('.')} and use '{callToActionKeyword}' to keep the conversation moving.";
    }

    private static string BuildRepurposeCallToAction(ClientProfile profile, string callToActionKeyword) =>
        RequiresBookingCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.CalendlyUrl)
            ? $"Book via {profile.CalendlyUrl}"
            : RequiresWebsiteCallToAction(profile) && !string.IsNullOrWhiteSpace(profile.WebsiteUrl)
                ? $"Visit {profile.WebsiteUrl}"
                : callToActionKeyword;

    private static string BuildEngagementPrompt(ClientProfile profile, string topic) =>
        RequiresBookingCallToAction(profile)
            ? $"Ask the audience what result they want before they book around {topic.ToLowerInvariant()}."
            : $"Ask the audience what part of {topic.ToLowerInvariant()} is slowing them down most.";

    private static bool RequiresBookingCallToAction(ClientProfile profile) =>
        ContainsAny(profile.DesiredAction, "book", "consult", "call", "appointment");

    private static bool RequiresWebsiteCallToAction(ClientProfile profile) =>
        ContainsAny(profile.DesiredAction, "website", "site", "web", "page", "landing", "visit");

    private static bool ContainsAny(string? value, params string[] fragments)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record LlmCanonicalContentFramePayload(
        string PrimaryHook,
        IReadOnlyList<LlmHookVariant> HookVariants,
        string CoreMessage,
        string Body,
        string Payoff,
        string CallToAction,
        string EngagementPrompt,
        string DesiredActionPrompt,
        string LanguageGuidance,
        string LanguageFormatInstruction,
        string ProductionNotes,
        IReadOnlyList<LlmRepurposeDirective> RepurposeDirectives);

    private sealed record LlmHookVariant(string Label, string Text);

    private sealed record LlmRepurposeDirective(string Format, string Intent, string Prompt);
}
