using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Application.Strategy;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Strategy;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class StrategyPlanningUseCasesTests
{
    [Fact]
    public void BuildStrategicProfileUseCase_DerivesConversionSignalsFromClientProfile()
    {
        var useCase = new BuildStrategicProfileUseCase();
        var profile = new ClientProfile(
                "Acme Tax",
                "Maria Rivera",
                "maria@acme.test",
                "Tax planning",
                "Small business tax planning",
                "Small business owners",
                "Professional",
                "BOOK",
                ["Instagram", "LinkedIn"],
                ["Unexpected tax bills", "Confusing compliance deadlines"],
                ["I already have an accountant"],
                ["Guarantees"],
                CalendlyUrl: "https://calendly.com/acme/consult",
                WebsiteUrl: "https://acme.test",
                MainGoal: "Generate more booked consultations",
                DesiredAction: "Book a consultation from the content",
                ContentLanguage: "Bilingual",
                ContentPlanTier: "Growth")
            .Normalize();

        var result = useCase.Execute(profile);

        Assert.Equal(StrategyConversionMode.Booking, result.ConversionMode);
        Assert.Equal("book_consultation", result.LeadGoal);
        Assert.Equal("booking a consultation through the scheduling link", result.ConversionDestination);
        Assert.Equal("Generate more booked consultations", result.MainGoal);
        Assert.Equal("Small business owners", result.TargetAudience);
        Assert.Equal(ContentPlanTier.Growth, result.ContentPlanTier);
    }

    [Fact]
    public void BuildStrategicProfileUseCase_NormalizesRawClientProfileBeforeDerivingStrategy()
    {
        var useCase = new BuildStrategicProfileUseCase();
        var rawProfile = new ClientProfile(
            "  Acme Tax  ",
            "Maria Rivera",
            "maria@acme.test",
            "  Tax planning. ",
            "",
            "",
            "",
            "",
            [],
            [],
            [],
            []);

        var result = useCase.Execute(rawProfile);

        Assert.Equal("Acme Tax", result.BusinessName);
        Assert.Equal("Tax planning", result.Niche);
        Assert.Equal("Growth services", result.Offer);
        Assert.Equal("Business owners", result.TargetAudience);
        Assert.Equal("BOOK", result.CallToActionKeyword);
        Assert.NotEmpty(result.PainPoints);
        Assert.NotEmpty(result.Objections);
        Assert.Equal(ContentPlanTier.Starter, result.ContentPlanTier);
    }

    [Fact]
    public void GenerateStrategyBlueprintUseCase_BuildsTenantSpecificPillarsAndBacklogBlueprint()
    {
        var useCase = new GenerateStrategyBlueprintUseCase();
        var strategicProfile = new StrategicProfile(
            "RNM Growth",
            "Marketing consulting",
            "Lead-gen content systems",
            "Founders with inconsistent outreach",
            "Professional",
            "English",
            "Start more inbound conversations",
            "Comment BOOK to get the next step",
            "BOOK",
            string.Empty,
            string.Empty,
            "Start more inbound conversations",
            "commenting with the keyword BOOK",
            "comment_keyword",
            StrategyConversionMode.CommentKeyword,
            ["Instagram"],
            ["Inconsistent demand", "Weak differentiation"],
            ["I do not want to be pushy"],
            Array.Empty<string>(),
            ContentPlanTier.Growth);

        var blueprint = useCase.Execute(strategicProfile, 14);

        Assert.Equal(1, blueprint.DailyPostingCadenceDays);
        Assert.Equal(2, blueprint.VideoCadenceDays);
        Assert.Equal(16, blueprint.MonthlyVideoTarget);
        Assert.Equal(ContentPlanTier.Growth, blueprint.ContentPlanTier);
        Assert.Equal(14, blueprint.BacklogBlueprintItems.Count);
        Assert.Contains("Lead-gen content systems strategy for Founders with inconsistent outreach in Marketing consulting", blueprint.ContentPillars);
        Assert.Contains("Conversion content that moves Founders with inconsistent outreach into comment-driven lead capture", blueprint.ContentPillars);
        Assert.Contains(blueprint.BacklogBlueprintItems, item => item.Category == ContentCategory.CtaDriven && item.UsesCallToActionKeyword);
        Assert.All(blueprint.BacklogBlueprintItems, item => Assert.Equal("comment_keyword", item.LeadGoal));
    }

    [Fact]
    public void GenerateStrategyBlueprintUseCase_ClampsWindowDays()
    {
        var useCase = new GenerateStrategyBlueprintUseCase();
        var strategicProfile = new StrategicProfile(
            "RNM Growth",
            "Marketing consulting",
            "Lead-gen content systems",
            "Founders",
            "Professional",
            "English",
            "Generate more leads",
            "Visit the website",
            "BOOK",
            string.Empty,
            "https://rnm.test",
            "Generate more leads",
            "visiting the website and taking the next step there",
            "visit_website",
            StrategyConversionMode.Website,
            ["Instagram"],
            ["Low visibility"],
            ["We already post"],
            Array.Empty<string>(),
            ContentPlanTier.Premium);

        var blueprint = useCase.Execute(strategicProfile, 99);

        Assert.Equal(28, blueprint.BacklogBlueprintItems.Count);
        Assert.Equal(1, blueprint.VideoCadenceDays);
        Assert.Equal(30, blueprint.MonthlyVideoTarget);
        Assert.All(blueprint.BacklogBlueprintItems, item => Assert.Equal("visit_website", item.LeadGoal));
    }

    [Fact]
    public void GenerateStrategyBlueprintUseCase_UsesFallbackSignalsWhenProfileListsAreEmpty()
    {
        var useCase = new GenerateStrategyBlueprintUseCase();
        var strategicProfile = new StrategicProfile(
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            StrategyConversionMode.Generic,
            [],
            [],
            [],
            [],
            ContentPlanTier.Starter);

        var blueprint = useCase.Execute(strategicProfile, 7);

        Assert.Equal(7, blueprint.BacklogBlueprintItems.Count);
        Assert.Equal(4, blueprint.VideoCadenceDays);
        Assert.Equal(8, blueprint.MonthlyVideoTarget);
        Assert.NotEmpty(blueprint.ContentPillars);
        Assert.Contains("Growth services strategy for Business owners in General Business", blueprint.ContentPillars);
        Assert.Contains(blueprint.BacklogBlueprintItems, item => item.Topic.Contains("low visibility", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateStrategyBlueprintUseCase_ExecuteAsync_UsesLlmBlueprintAndPreservesPlanPolicy()
    {
        var planner = new FakeStrategyPlanner(
            StrategyPlannerResult.Success(
                """
                {
                  "strategicNarrative": "RNM Growth should publish niche-specific authority content.",
                  "contentPillars": [
                    "Lead-gen systems for founders",
                    "Objection handling for inbound demand",
                    "Conversion content for comment-led capture"
                  ],
                  "backlogBlueprintItems": [
                    {
                      "sequence": 1,
                      "plannedOffsetDays": 0,
                      "category": "Authority",
                      "primaryFormat": "ShortVideo",
                      "topic": "Founder positioning",
                      "angle": "Show the smarter authority play",
                      "hookDirection": "Lead with the overlooked credibility gap",
                      "leadGoal": "comment_keyword",
                      "usesCallToActionKeyword": true
                    },
                    {
                      "sequence": 2,
                      "plannedOffsetDays": 1,
                      "category": "Comparison",
                      "primaryFormat": "BrandedGraphic",
                      "topic": "Inbound conversations",
                      "angle": "Compare passive posting with demand capture",
                      "hookDirection": "Open with the side-by-side contrast",
                      "leadGoal": "comment_keyword",
                      "usesCallToActionKeyword": false
                    },
                    {
                      "sequence": 3,
                      "plannedOffsetDays": 2,
                      "category": "CtaDriven",
                      "primaryFormat": "ShortVideo",
                      "topic": "Comment BOOK timing",
                      "angle": "Show when the keyword CTA works best",
                      "hookDirection": "Open with a CTA-oriented curiosity gap",
                      "leadGoal": "comment_keyword",
                      "usesCallToActionKeyword": true
                    },
                    {
                      "sequence": 4,
                      "plannedOffsetDays": 3,
                      "category": "PainPoint",
                      "primaryFormat": "BrandedGraphic",
                      "topic": "Weak demand flow",
                      "angle": "Expose the cost of inconsistent outreach",
                      "hookDirection": "Open by naming the bottleneck",
                      "leadGoal": "comment_keyword",
                      "usesCallToActionKeyword": false
                    },
                    {
                      "sequence": 5,
                      "plannedOffsetDays": 4,
                      "category": "Faq",
                      "primaryFormat": "BrandedGraphic",
                      "topic": "What buyers ask before engaging",
                      "angle": "Answer the trust-building question buyers already have",
                      "hookDirection": "Use a question-based opening",
                      "leadGoal": "comment_keyword",
                      "usesCallToActionKeyword": false
                    },
                    {
                      "sequence": 6,
                      "plannedOffsetDays": 5,
                      "category": "Story",
                      "primaryFormat": "ShortVideo",
                      "topic": "Founder turnaround story",
                      "angle": "Show the shift from inconsistent outreach to demand capture",
                      "hookDirection": "Open with a vivid before-and-after moment",
                      "leadGoal": "comment_keyword",
                      "usesCallToActionKeyword": false
                    },
                    {
                      "sequence": 7,
                      "plannedOffsetDays": 6,
                      "category": "Urgency",
                      "primaryFormat": "BrandedGraphic",
                      "topic": "Cost of waiting",
                      "angle": "Show how delay makes positioning harder",
                      "hookDirection": "Lead with the cost of waiting",
                      "leadGoal": "comment_keyword",
                      "usesCallToActionKeyword": true
                    }
                  ]
                }
                """,
                "gpt-5-mini",
                "strategy-blueprint-v1"));
        var validator = new FakeGuardrailValidator(GuardrailValidationResult.Valid());
        var useCase = new GenerateStrategyBlueprintUseCase(planner, validator);
        var strategicProfile = new StrategicProfile(
            "RNM Growth",
            "Marketing consulting",
            "Lead-gen content systems",
            "Founders with inconsistent outreach",
            "Professional",
            "English",
            "Start more inbound conversations",
            "Comment BOOK to get the next step",
            "BOOK",
            string.Empty,
            string.Empty,
            "Start more inbound conversations",
            "commenting with the keyword BOOK",
            "comment_keyword",
            StrategyConversionMode.CommentKeyword,
            ["Instagram"],
            ["Inconsistent demand", "Weak differentiation"],
            ["I do not want to be pushy"],
            Array.Empty<string>(),
            ContentPlanTier.Starter);

        var blueprint = await useCase.ExecuteAsync(strategicProfile, 7, CancellationToken.None);

        Assert.Equal("RNM Growth should publish niche-specific authority content.", blueprint.StrategicNarrative);
        Assert.Equal(4, blueprint.VideoCadenceDays);
        Assert.Equal(8, blueprint.MonthlyVideoTarget);
        Assert.Equal(7, blueprint.BacklogBlueprintItems.Count);
        Assert.Equal(ContentCategory.Authority, blueprint.BacklogBlueprintItems[0].Category);
        Assert.Equal(PrimaryFormat.ShortVideo, blueprint.BacklogBlueprintItems[0].PrimaryFormat);
    }

    [Fact]
    public async Task GenerateStrategyBlueprintUseCase_ExecuteAsync_FallsBackWhenLlmPayloadIsInvalid()
    {
        var planner = new FakeStrategyPlanner(
            StrategyPlannerResult.Success(
                """{"strategicNarrative":"bad","contentPillars":["Only one"],"backlogBlueprintItems":[]}""",
                "gpt-5-mini",
                "strategy-blueprint-v1"));
        var validator = new FakeGuardrailValidator(GuardrailValidationResult.Valid());
        var useCase = new GenerateStrategyBlueprintUseCase(planner, validator);
        var strategicProfile = new StrategicProfile(
            "RNM Growth",
            "Marketing consulting",
            "Lead-gen content systems",
            "Founders",
            "Professional",
            "English",
            "Generate more leads",
            "Visit the website",
            "BOOK",
            string.Empty,
            "https://rnm.test",
            "Generate more leads",
            "visiting the website and taking the next step there",
            "visit_website",
            StrategyConversionMode.Website,
            ["Instagram"],
            ["Low visibility"],
            ["We already post"],
            Array.Empty<string>(),
            ContentPlanTier.Premium);

        var blueprint = await useCase.ExecuteAsync(strategicProfile, 7, CancellationToken.None);

        Assert.Equal(1, blueprint.VideoCadenceDays);
        Assert.Equal(30, blueprint.MonthlyVideoTarget);
        Assert.Equal(7, blueprint.BacklogBlueprintItems.Count);
        Assert.Contains(blueprint.ContentPillars, pillar => pillar.Contains("Conversion content", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeStrategyPlanner(StrategyPlannerResult result) : ILLMStrategyPlanner
    {
        public Task<StrategyPlannerResult> GenerateAsync(StrategyPlannerRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class FakeGuardrailValidator(GuardrailValidationResult result) : IContentGuardrailValidator
    {
        public Task<GuardrailValidationResult> ValidateAsync(ContentGuardrailValidationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
