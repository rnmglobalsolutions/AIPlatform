using System.Text.RegularExpressions;
using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.Common;
using AIMultiAgentPlatform.Contracts.Intake;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Strategy;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Intake;

public sealed class ProcessTallySubmissionUseCase
{
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

    private readonly ITenantRepository _tenantRepository;
    private readonly IStrategyPlanRepository _strategyPlanRepository;
    private readonly IEditorialBacklogRepository _editorialBacklogRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public ProcessTallySubmissionUseCase(
        ITenantRepository tenantRepository,
        IStrategyPlanRepository strategyPlanRepository,
        IEditorialBacklogRepository editorialBacklogRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _tenantRepository = tenantRepository;
        _strategyPlanRepository = strategyPlanRepository;
        _editorialBacklogRepository = editorialBacklogRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<TallySubmissionResponse>> ExecuteAsync(
        ProcessTallySubmissionCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Submission;
        if (string.IsNullOrWhiteSpace(request.BusinessName))
        {
            return Result<TallySubmissionResponse>.Failure("intake.business-name.required", "Business name is required.");
        }

        var profile = new ClientProfile(
                request.BusinessName,
                request.PrimaryContactName,
                request.PrimaryContactEmail,
                request.Niche,
                request.Offer,
                request.TargetAudience,
                request.BrandTone,
                request.CallToActionKeyword,
                request.Platforms,
                request.PainPoints,
                request.Objections,
                request.AvoidTopics)
            .Normalize();

        var slug = Slugify(profile.BusinessName);
        var tenantId = new TenantId(_idGenerator.NewId("tenant"));
        var tenant = Tenant.Create(tenantId, slug, profile, _clock.UtcNow);

        var strategyPlan = new StrategyPlan(
            _idGenerator.NewId("strategy"),
            tenantId,
            $"{profile.BusinessName} should publish daily educational content that turns audience pain points into authority and lead conversations.",
            BuildPillars(profile),
            1,
            3,
            _clock.UtcNow);

        var windowDays = Math.Clamp(request.BacklogWindowDays, 7, 28);
        var backlog = new EditorialBacklog(
            _idGenerator.NewId("backlog"),
            tenantId,
            windowDays,
            _clock.UtcNow,
            BuildBacklog(profile, windowDays));

        await _tenantRepository.SaveAsync(tenant, cancellationToken);
        await _strategyPlanRepository.SaveAsync(strategyPlan, cancellationToken);
        await _editorialBacklogRepository.SaveAsync(backlog, cancellationToken);

        return Result<TallySubmissionResponse>.Success(
            new TallySubmissionResponse(
                tenantId.Value,
                slug,
                strategyPlan.StrategyPlanId,
                backlog.EditorialBacklogId,
                backlog.Items.Count));
    }

    private static IReadOnlyList<string> BuildPillars(ClientProfile profile) =>
    [
        $"{profile.Niche} authority",
        "Pain point education",
        "Objection handling",
        $"Lead generation around {profile.Offer}"
    ];

    private static IReadOnlyList<EditorialBacklogItem> BuildBacklog(ClientProfile profile, int windowDays)
    {
        var items = new List<EditorialBacklogItem>(windowDays);
        for (var day = 0; day < windowDays; day++)
        {
            var category = CategoryRotation[day % CategoryRotation.Length];
            var primaryFormat = (day + 1) % 3 == 0 ? PrimaryFormat.ShortVideo : PrimaryFormat.BrandedGraphic;
            var anchor = ResolveAnchor(profile, category, day);
            items.Add(
                new EditorialBacklogItem(
                    day + 1,
                    day,
                    category,
                    primaryFormat,
                    anchor.Topic,
                    anchor.Angle,
                    anchor.HookDirection,
                    "comments_or_dms",
                    true));
        }

        return items;
    }

    private static (string Topic, string Angle, string HookDirection) ResolveAnchor(ClientProfile profile, ContentCategory category, int index)
    {
        var painPoint = profile.PainPoints[index % profile.PainPoints.Count];
        var objection = profile.Objections[index % profile.Objections.Count];

        return category switch
        {
            ContentCategory.PainPoint => ($"{painPoint} in {profile.Niche}", $"Expose the hidden cost of {painPoint.ToLowerInvariant()}", "Call out the pain directly"),
            ContentCategory.Mistake => ($"Mistakes businesses make around {profile.Offer}", $"Show the mistake behind weak results", "Lead with the most common mistake"),
            ContentCategory.MythBusting => ($"Myths about {profile.Offer}", $"Challenge a false belief blocking action", "Break a common myth"),
            ContentCategory.Faq => ($"Frequently asked question about {profile.Offer}", $"Answer the question in a simple way", "Use a question-based hook"),
            ContentCategory.ObjectionHandling => ($"Objection: {objection}", $"Reframe the objection and remove friction", "Address the objection head-on"),
            ContentCategory.Authority => ($"Authority insight for {profile.Niche}", $"Teach a nuanced insight decision-makers miss", "Reveal what experts know"),
            ContentCategory.Story => ($"Story from the world of {profile.Niche}", $"Use a mini-story to create emotional relevance", "Open with a relatable scenario"),
            ContentCategory.Comparison => ($"Comparison inside {profile.Niche}", $"Contrast the ineffective path with the effective one", "Use before-vs-after contrast"),
            ContentCategory.CtaDriven => ($"Lead generation angle for {profile.Offer}", $"Build curiosity around the CTA keyword {profile.CallToActionKeyword}", "Use a CTA-oriented curiosity gap"),
            _ => ($"Urgency around {profile.Offer}", $"Show what happens if the audience delays action", "Lead with the cost of waiting")
        };
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9]+", "-");
        normalized = Regex.Replace(normalized, @"-+", "-");
        return normalized.Trim('-');
    }
}
