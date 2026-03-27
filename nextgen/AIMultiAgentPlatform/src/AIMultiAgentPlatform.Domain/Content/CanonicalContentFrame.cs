using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Editorial;

namespace AIMultiAgentPlatform.Domain.Content;

public sealed record CanonicalContentFrame(
    TenantId TenantId,
    ContentCategory Category,
    PrimaryFormat PrimaryFormat,
    string Topic,
    string Angle,
    string HookDirection,
    string PrimaryHook,
    IReadOnlyList<HookVariant> HookVariants,
    string CoreMessage,
    string Body,
    string Payoff,
    string CallToAction,
    string EngagementPrompt,
    string DesiredActionPrompt,
    string CallToActionKeyword,
    string LanguageGuidance,
    string LanguageFormatInstruction,
    string ProductionNotes,
    IReadOnlyList<RepurposeDirective> RepurposeDirectives,
    IReadOnlyList<string> RecentTopicsToAvoid,
    IReadOnlyList<string> RecentHooksToAvoid);
