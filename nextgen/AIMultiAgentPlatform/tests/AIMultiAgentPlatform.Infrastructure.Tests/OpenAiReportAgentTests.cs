using System.Net;
using System.Text;
using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Domain.Common;
using AIMultiAgentPlatform.Domain.Reporting;
using AIMultiAgentPlatform.Domain.Tenants;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Reporting;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class OpenAiReportAgentTests
{
    [Fact]
    public async Task GenerateAsync_UsesOpenAiPayloadWhenResponseIsValid()
    {
        string? capturedRequestBody = null;
        var handler = new FakeHttpMessageHandler(async request =>
        {
            capturedRequestBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"{\"executiveSummary\":\"LLM executive\",\"operationalSummary\":\"LLM operational\",\"recommendations\":[{\"title\":\"Scale Instagram\",\"priority\":\"High\",\"rationale\":\"It is working.\",\"recommendedAction\":\"Publish more there.\",\"supportingMetric\":\"Top platform: Instagram\"}]}"}}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var agent = new OpenAiReportAgent(
            new OpenAiOptions(true, "https://example.openai.azure.com/openai/v1", "key_123", "gpt-5-mini", "gpt-5-mini", 4000, 6000),
            new HeuristicReportAgent(),
            httpClient);

        var result = await agent.GenerateAsync(CreateContext(), CancellationToken.None);

        Assert.Equal("LLM executive", result.ExecutiveSummary);
        Assert.Equal("LLM operational", result.OperationalSummary);
        Assert.Single(result.Recommendations);
        Assert.Equal("Scale Instagram", result.Recommendations[0].Title);
        Assert.NotNull(capturedRequestBody);
        Assert.Contains("Provider coverage notes", capturedRequestBody, StringComparison.Ordinal);
        Assert.Contains("Top content by business impact", capturedRequestBody, StringComparison.Ordinal);
        Assert.Contains("Metricool", capturedRequestBody, StringComparison.Ordinal);
        Assert.Contains("posts have reconciled metric snapshots", capturedRequestBody, StringComparison.Ordinal);
    }

    private static ReportGenerationContext CreateContext()
    {
        var tenant = Tenant.Create(
            new TenantId("tenant_001"),
            "rnm-growth",
            new ClientProfile(
                "RNM Growth",
                "Jane Doe",
                "jane@rnm.test",
                "Agencies",
                "Growth systems",
                "Founders",
                "Bold",
                "BOOK",
                ["Instagram"],
                ["Low visibility"],
                ["No time"],
                Array.Empty<string>()),
            DateTime.UtcNow);

        return new ReportGenerationContext(
            tenant,
            new MonthlyPerformanceSnapshot(
                "snapshot_001",
                tenant.TenantId,
                "2026-03",
                "English",
                "Book a call",
                8,
                4,
                1,
                "Short video: Authority",
                8.7,
                7,
                1,
                5,
                4,
                3,
                2,
                4,
                3,
                21,
                14,
                DateTime.UtcNow,
                5000,
                14,
                8,
                6,
                4,
                3,
                2),
            new MonthlyPerformanceSource(
                [],
                [new Domain.Content.PrimaryAsset("primary_001", "request_001", tenant.TenantId, Domain.Editorial.PrimaryFormat.ShortVideo, "Winning Reel", "Hook", "Body", "Payoff", "CTA", "Notes")],
                [],
                [],
                [],
                [
                    new Domain.Publishing.PublishedContentRecord("published_001", "request_001", "schedule_001", tenant.TenantId, "Metricool", "Instagram", "profile_001", "post_001", string.Empty, "Winning Reel caption", "https://blob.test/video.mp4", Domain.Publishing.PublishedContentStatus.Published, string.Empty, DateTime.UtcNow),
                    new Domain.Publishing.PublishedContentRecord("published_002", "request_002", "schedule_002", tenant.TenantId, "Buffer", "LinkedIn", "profile_002", "post_002", string.Empty, "Weak LinkedIn caption", "https://blob.test/video.mp4", Domain.Publishing.PublishedContentStatus.Published, string.Empty, DateTime.UtcNow)
                ],
                [
                    new Domain.Leads.LeadProfile("lead_001", tenant.TenantId, "contact_001", "Jane", "Doe", "jane@rnm.test", "Instagram", Domain.Leads.LeadLifecycleStage.BookingReady, "Intent", "BOOK", DateTime.UtcNow, "published_001", "Instagram", "Metricool", "post_001")
                ],
                [
                    new Domain.Booking.BookingRecord("booking_001", tenant.TenantId, "lead_001", "contact_001", Domain.Booking.BookingStatus.Booked, "https://calendly.test", "strategy-call", DateTime.UtcNow, DateTime.UtcNow, "published_001", "Instagram", "Metricool")
                ],
                [],
                [],
                [
                    new Domain.Publishing.PublishedContentMetricSnapshot("metric_001", "published_001", tenant.TenantId, "Metricool", "Instagram", "published", 4200, 32, 18, 6, 5, DateTime.UtcNow),
                    new Domain.Publishing.PublishedContentMetricSnapshot("metric_002", "published_002", tenant.TenantId, "Buffer", "LinkedIn", "published", 1500, 4, 1, 0, 0, DateTime.UtcNow)
                ]));
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}
