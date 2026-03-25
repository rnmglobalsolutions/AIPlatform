using AIMultiAgentPlatform.Application.Abstractions;
using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Application.DailyContent;
using AIMultiAgentPlatform.Contracts.Content;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Domain.Editorial;
using AIMultiAgentPlatform.Domain.Tenants;

namespace AIMultiAgentPlatform.Application.Tests;

public sealed class GenerateDailyContentPackageUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_GeneratesDailyContentArtifacts()
    {
        var tenant = Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "B2B consultants",
                "Content-led growth",
                "Founders",
                "Bold",
                "BOOK",
                ["Instagram", "LinkedIn"],
                ["Low visibility", "Weak pipeline"],
                ["No time", "Unsure what to post"],
                ["Politics"]),
            new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc));

        var backlog = new EditorialBacklog(
            "backlog_001",
            tenant.TenantId,
            14,
            DateTime.UtcNow,
            [new EditorialBacklogItem(3, 2, ContentCategory.Authority, PrimaryFormat.ShortVideo, "Authority insight", "Teach the smarter move", "Open with the hidden edge", "comments_or_dms", true)]);

        var tenantRepository = new FakeTenantRepository(tenant);
        var backlogRepository = new FakeEditorialBacklogRepository(backlog);
        var requestRepository = new FakeDailyContentRequestRepository();
        var briefRepository = new FakeDailyContentBriefRepository();
        var primaryAssetRepository = new FakePrimaryAssetRepository();
        var captionAssetRepository = new FakeCaptionAssetRepository();
        var bundleRepository = new FakeRepurposedAssetBundleRepository();

        var useCase = new GenerateDailyContentPackageUseCase(
            tenantRepository,
            backlogRepository,
            requestRepository,
            briefRepository,
            primaryAssetRepository,
            captionAssetRepository,
            bundleRepository,
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(
                new GenerateDailyContentPackageRequest(tenant.TenantId.Value, backlog.EditorialBacklogId, 3, "corr-123"),
                "corr-123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("daily_request_001", result.Value!.DailyContentRequestId);
        Assert.Equal("ShortVideo", result.Value.PrimaryFormat);
        Assert.Contains("HeyGen-compatible", primaryAssetRepository.Saved!.ProductionNotes);
        Assert.Equal("BOOK", captionAssetRepository.Saved!.CallToActionKeyword);
        Assert.Equal(3, bundleRepository.Saved!.StoryFrames.Count);
        Assert.Equal("corr-123", requestRepository.Saved!.CorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailureWhenBacklogSequenceMissing()
    {
        var tenant = Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "B2B consultants",
                "Content-led growth",
                "Founders",
                "Bold",
                "BOOK",
                ["Instagram"],
                ["Low visibility"],
                ["No time"],
                []),
            DateTime.UtcNow);

        var backlog = new EditorialBacklog(
            "backlog_001",
            tenant.TenantId,
            14,
            DateTime.UtcNow,
            [new EditorialBacklogItem(1, 0, ContentCategory.PainPoint, PrimaryFormat.BrandedGraphic, "Topic", "Angle", "Hook", "comments_or_dms", true)]);

        var useCase = new GenerateDailyContentPackageUseCase(
            new FakeTenantRepository(tenant),
            new FakeEditorialBacklogRepository(backlog),
            new FakeDailyContentRequestRepository(),
            new FakeDailyContentBriefRepository(),
            new FakePrimaryAssetRepository(),
            new FakeCaptionAssetRepository(),
            new FakeRepurposedAssetBundleRepository(),
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(
                new GenerateDailyContentPackageRequest(tenant.TenantId.Value, backlog.EditorialBacklogId, 7)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("daily-content.sequence.not-found", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_UsesTenantLanguageAndDesiredActionInGeneratedContent()
    {
        var tenant = Tenant.Create(
            new TenantId("tenant_002"),
            "acme-tax",
            new ClientProfile(
                "Acme Tax",
                "John Doe",
                "john@acme.test",
                "Tax services",
                "Tax planning",
                "Small business owners",
                "Professional",
                "BOOK",
                ["Instagram", "LinkedIn"],
                ["Confusing deadlines"],
                ["Too expensive"],
                ["Politics"],
                CalendlyUrl: "https://calendly.com/acme-tax/consultation",
                MainGoal: "Book more consultations",
                DesiredAction: "Book a consultation from the content",
                ContentLanguage: "Bilingual"),
            DateTime.UtcNow);

        var backlog = new EditorialBacklog(
            "backlog_002",
            tenant.TenantId,
            14,
            DateTime.UtcNow,
            [new EditorialBacklogItem(1, 0, ContentCategory.Authority, PrimaryFormat.ShortVideo, "Tax planning", "Show the planning shortcut", "Lead with the overlooked mistake", "comments_or_dms", true)]);

        var briefRepository = new FakeDailyContentBriefRepository();
        var primaryAssetRepository = new FakePrimaryAssetRepository();
        var captionAssetRepository = new FakeCaptionAssetRepository();
        var bundleRepository = new FakeRepurposedAssetBundleRepository();

        var useCase = new GenerateDailyContentPackageUseCase(
            new FakeTenantRepository(tenant),
            new FakeEditorialBacklogRepository(backlog),
            new FakeDailyContentRequestRepository(),
            briefRepository,
            primaryAssetRepository,
            captionAssetRepository,
            bundleRepository,
            new DeterministicIdGenerator(),
            new FixedClock());

        var result = await useCase.ExecuteAsync(
            new GenerateDailyContentPackageCommand(
                new GenerateDailyContentPackageRequest(tenant.TenantId.Value, backlog.EditorialBacklogId, 1)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("bilingual format", briefRepository.Saved!.CoreMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("book directly through https://calendly.com/acme-tax/consultation", primaryAssetRepository.Saved!.CallToAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("English and Spanish", primaryAssetRepository.Saved.ProductionNotes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://calendly.com/acme-tax/consultation", captionAssetRepository.Saved!.Caption, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Book via https://calendly.com/acme-tax/consultation", bundleRepository.Saved!.CarouselOutline, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DeterministicIdGenerator : IIdGenerator
    {
        private int _sequence;

        public string NewId(string prefix)
        {
            _sequence++;
            return $"{prefix}_{_sequence:000}";
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeTenantRepository(Tenant tenant) : ITenantRepository
    {
        public Task SaveAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Tenant?> FindByIdAsync(string tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(tenant.TenantId.Value == tenantId ? tenant : null);
    }

    private sealed class FakeEditorialBacklogRepository(EditorialBacklog backlog) : IEditorialBacklogRepository
    {
        public Task SaveAsync(EditorialBacklog backlog, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<EditorialBacklog?> FindByIdAsync(string backlogId, CancellationToken cancellationToken) =>
            Task.FromResult(backlog.EditorialBacklogId == backlogId ? backlog : null);
    }

    private sealed class FakeDailyContentRequestRepository : IDailyContentRequestRepository
    {
        public DailyContentRequest? Saved { get; private set; }

        public Task SaveAsync(DailyContentRequest request, CancellationToken cancellationToken)
        {
            Saved = request;
            return Task.CompletedTask;
        }

        public Task<DailyContentRequest?> FindByIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
    }

    private sealed class FakeDailyContentBriefRepository : IDailyContentBriefRepository
    {
        public DailyContentBrief? Saved { get; private set; }

        public Task SaveAsync(DailyContentBrief brief, CancellationToken cancellationToken)
        {
            Saved = brief;
            return Task.CompletedTask;
        }

        public Task<DailyContentBrief?> FindByIdAsync(string briefId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentBriefId == briefId ? Saved : null);

        public Task<DailyContentBrief?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
    }

    private sealed class FakePrimaryAssetRepository : IPrimaryAssetRepository
    {
        public PrimaryAsset? Saved { get; private set; }

        public Task SaveAsync(PrimaryAsset asset, CancellationToken cancellationToken)
        {
            Saved = asset;
            return Task.CompletedTask;
        }

        public Task<PrimaryAsset?> FindByIdAsync(string primaryAssetId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.PrimaryAssetId == primaryAssetId ? Saved : null);

        public Task<PrimaryAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
    }

    private sealed class FakeCaptionAssetRepository : ICaptionAssetRepository
    {
        public CaptionAsset? Saved { get; private set; }

        public Task SaveAsync(CaptionAsset asset, CancellationToken cancellationToken)
        {
            Saved = asset;
            return Task.CompletedTask;
        }

        public Task<CaptionAsset?> FindByIdAsync(string captionAssetId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.CaptionAssetId == captionAssetId ? Saved : null);

        public Task<CaptionAsset?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
    }

    private sealed class FakeRepurposedAssetBundleRepository : IRepurposedAssetBundleRepository
    {
        public RepurposedAssetBundle? Saved { get; private set; }

        public Task SaveAsync(RepurposedAssetBundle bundle, CancellationToken cancellationToken)
        {
            Saved = bundle;
            return Task.CompletedTask;
        }

        public Task<RepurposedAssetBundle?> FindByIdAsync(string bundleId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.RepurposedAssetBundleId == bundleId ? Saved : null);

        public Task<RepurposedAssetBundle?> FindByRequestIdAsync(string requestId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.DailyContentRequestId == requestId ? Saved : null);
    }
}
