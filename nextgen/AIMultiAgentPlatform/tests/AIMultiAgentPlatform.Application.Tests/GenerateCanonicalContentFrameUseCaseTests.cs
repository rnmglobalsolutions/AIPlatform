using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Application.Content;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class GenerateCanonicalContentFrameUseCaseTests
{
    [Fact]
    public void Execute_BuildsFrameWithAntiRepetitionGuidanceAndRepurposeDirectives()
    {
        var useCase = new GenerateCanonicalContentFrameUseCase();
        var profile = new ClientProfile(
            "Acme Tax",
            "Maria Rivera",
            "maria@acme.test",
            "Tax planning",
            "Advisory retainers",
            "Small business owners",
            "Professional",
            "BOOK",
            ["Instagram", "LinkedIn"],
            ["Unexpected tax bills"],
            ["I already have an accountant"],
            [],
            CalendlyUrl: "https://calendly.com/acme/consult",
            MainGoal: "Book more consultations",
            DesiredAction: "Book a consultation from the content",
            ContentLanguage: "Bilingual");
        var backlogItem = new EditorialBacklogItem(
            1,
            0,
            ContentCategory.Authority,
            PrimaryFormat.ShortVideo,
            "Tax planning shortcuts",
            "Show the planning shortcut",
            "Lead with the overlooked mistake",
            "book_consultation",
            true);
        var snapshot = new ContentMemorySnapshot(
            new TenantId("tenant_001"),
            new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc),
            [
                new ContentMemoryEntry(
                    "content_memory_001",
                    new TenantId("tenant_001"),
                    "PrimaryAsset",
                    "primary_001",
                    "Quarterly tax checklists",
                    "Start with the hidden penalty",
                    "Book a consultation",
                    "book_consultation",
                    "Instagram",
                    "hash-001",
                    new DateTime(2026, 03, 20, 12, 0, 0, DateTimeKind.Utc),
                    ContentMemoryLifecycleStage.Published)
            ],
            ["Quarterly tax checklists"],
            ["Start with the hidden penalty"],
            ["Book a consultation"],
            ["Instagram"],
            ["book_consultation"],
            ["hash-001"]);

        var frame = useCase.Execute(new TenantId("tenant_001"), profile, backlogItem, snapshot);

        Assert.Equal("Tax planning shortcuts", frame.Topic);
        Assert.Contains("bilingual format", frame.LanguageGuidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Avoid repeating recent topics like quarterly tax checklists", frame.CoreMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Avoid reusing hook patterns like start with the hidden penalty", frame.CoreMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HeyGen-compatible", frame.ProductionNotes, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, frame.HookVariants.Count);
        Assert.Equal(3, frame.RepurposeDirectives.Count);
        Assert.Contains(frame.RepurposeDirectives, directive => directive.Format == "Carousel");
        Assert.Equal(["Quarterly tax checklists"], frame.RecentTopicsToAvoid);
        Assert.Equal(["Start with the hidden penalty"], frame.RecentHooksToAvoid);
    }

    [Fact]
    public void Execute_UsesWebsiteCallToActionWhenDesiredActionRequiresWebsite()
    {
        var useCase = new GenerateCanonicalContentFrameUseCase();
        var profile = new ClientProfile(
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "Marketing consulting",
            "Strategy retainers",
            "Founders",
            "Bold",
            "BOOK",
            ["Instagram"],
            ["Low visibility"],
            ["No time"],
            [],
            WebsiteUrl: "https://rnmgrowth.com",
            DesiredAction: "Visit the website to learn more");
        var backlogItem = new EditorialBacklogItem(
            2,
            1,
            ContentCategory.Authority,
            PrimaryFormat.BrandedGraphic,
            "Content positioning",
            "Show the smarter growth path",
            "Lead with the hidden cost of waiting",
            "visit_website",
            true);

        var frame = useCase.Execute(new TenantId("tenant_002"), profile, backlogItem);

        Assert.Contains("https://rnmgrowth.com", frame.CallToAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://rnmgrowth.com", frame.DesiredActionPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(frame.RepurposeDirectives, directive => directive.Prompt.Contains("https://rnmgrowth.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_UsesLlmGeneratedFrameWhenPayloadIsValid()
    {
        var generator = new FakeContentGenerator(
            ContentGenerationResult.Success(
                """
                {
                  "primaryHook": "Your content is not broken. Your positioning is.",
                  "hookVariants": [
                    { "label": "primary", "text": "Your content is not broken. Your positioning is." },
                    { "label": "contrarian", "text": "More posting is not the fix if the message misses buyer intent." }
                  ],
                  "coreMessage": "Founders need clearer positioning before they need more volume.",
                  "body": "HOOK: Your content is not broken. Your positioning is. BODY: Explain why buyer intent matters before volume. PAYOFF: Show the next strategic shift.",
                  "payoff": "Leave the audience with a cleaner path to better inbound conversations.",
                  "callToAction": "Invite the audience to comment 'BOOK' for the next step.",
                  "engagementPrompt": "Ask the audience where their message feels weakest right now.",
                  "desiredActionPrompt": "Comment 'BOOK' if you want the next step.",
                  "languageGuidance": "Write the content in English.",
                  "languageFormatInstruction": "Use English-first phrasing.",
                  "productionNotes": "15-45 second HeyGen-compatible script with concise spoken phrasing.",
                  "repurposeDirectives": [
                    { "format": "Carousel", "intent": "Break the argument into a 5-slide sequence", "prompt": "Close with Comment 'BOOK' for the next step." },
                    { "format": "Stories", "intent": "Use three frames with tension, insight, CTA", "prompt": "Use a CTA sticker for COMMENT BOOK." }
                  ]
                }
                """,
                "gpt-5-mini",
                "canonical-content-frame-v1"));
        var validator = new FakeGuardrailValidator(GuardrailValidationResult.Valid());
        var useCase = new GenerateCanonicalContentFrameUseCase(generator, validator);
        var profile = new ClientProfile(
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "Marketing consulting",
            "Lead-gen systems",
            "Founders",
            "Bold",
            "BOOK",
            ["Instagram"],
            ["Low visibility"],
            ["No time"],
            []);
        var backlogItem = new EditorialBacklogItem(
            1,
            0,
            ContentCategory.Authority,
            PrimaryFormat.ShortVideo,
            "Positioning gaps",
            "Show the smarter growth path",
            "Lead with the hidden cost of waiting",
            "comment_keyword",
            true);

        var frame = await useCase.ExecuteAsync(new TenantId("tenant_001"), profile, backlogItem, cancellationToken: CancellationToken.None);

        Assert.Equal("Your content is not broken. Your positioning is.", frame.PrimaryHook);
        Assert.Equal(2, frame.HookVariants.Count);
        Assert.Equal(2, frame.RepurposeDirectives.Count);
        Assert.Contains("buyer intent", frame.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("BOOK", frame.CallToActionKeyword);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToHeuristicFrameWhenLlmPayloadIsInvalid()
    {
        var generator = new FakeContentGenerator(
            ContentGenerationResult.Success(
                """{"primaryHook":"Only this field exists"}""",
                "gpt-5-mini",
                "canonical-content-frame-v1"));
        var validator = new FakeGuardrailValidator(GuardrailValidationResult.Valid());
        var useCase = new GenerateCanonicalContentFrameUseCase(generator, validator);
        var profile = new ClientProfile(
            "RNM Growth",
            "Jane Doe",
            "jane@rnm.test",
            "Marketing consulting",
            "Lead-gen systems",
            "Founders",
            "Bold",
            "BOOK",
            ["Instagram"],
            ["Low visibility"],
            ["No time"],
            []);
        var backlogItem = new EditorialBacklogItem(
            1,
            0,
            ContentCategory.Authority,
            PrimaryFormat.ShortVideo,
            "Positioning gaps",
            "Show the smarter growth path",
            "Lead with the hidden cost of waiting",
            "comment_keyword",
            true);

        var frame = await useCase.ExecuteAsync(new TenantId("tenant_001"), profile, backlogItem, cancellationToken: CancellationToken.None);

        Assert.Contains("Lead with the hidden cost of waiting", frame.PrimaryHook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Show the smarter growth path", frame.CoreMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeContentGenerator(ContentGenerationResult result) : ILLMContentGenerator
    {
        public Task<ContentGenerationResult> GenerateAsync(ContentGenerationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class FakeGuardrailValidator(GuardrailValidationResult result) : IContentGuardrailValidator
    {
        public Task<GuardrailValidationResult> ValidateAsync(ContentGuardrailValidationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
