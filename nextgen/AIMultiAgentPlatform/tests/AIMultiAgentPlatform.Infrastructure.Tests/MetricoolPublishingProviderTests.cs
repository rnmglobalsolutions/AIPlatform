using System.Net;
using System.Text;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Publishing.Metricool;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class MetricoolPublishingProviderTests
{
    [Fact]
    public async Task PublishAsync_UsesConfiguredTemplateAndParsesIdentifiers()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":{"id":"metricool_post_123","url":"https://instagram.com/p/metricool-post"}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var provider = new MetricoolPublishingProvider(
            new MetricoolOptions(
                true,
                "https://metricool.test/api/",
                "v1/plans/{externalProfileId}/posts?network={platform}",
                "v1/posts/{externalPostId}/analytics",
                "Authorization",
                "Bearer",
                string.Empty),
            httpClient);

        var result = await provider.PublishAsync(
            new PublishingRequest(
                "tenant_001",
                "corr_001",
                "brand_123",
                "token_123",
                "Instagram",
                "Metricool caption copy",
                "https://blob.test/video.mp4",
                new DateTime(2026, 03, 27, 18, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("metricool_post_123", result.ExternalPostId);
        Assert.Equal("https://instagram.com/p/metricool-post", result.ExternalUrl);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://metricool.test/api/v1/plans/brand_123/posts?network=Instagram", capturedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer token_123", string.Join(",", capturedRequest.Headers.GetValues("Authorization")));
        Assert.NotNull(capturedBody);
        Assert.Contains("\"externalProfileId\":\"brand_123\"", capturedBody, StringComparison.Ordinal);
        Assert.Contains("\"mediaUrl\":\"https://blob.test/video.mp4\"", capturedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReconcileAsync_ParsesFlexibleAnalyticsPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":{"state":"published","permalink":"https://instagram.com/p/metricool-post","publishedAt":"2026-03-27T18:30:00Z","analytics":{"impressions":3200,"linkClicks":44,"reactions":21,"comments":4,"reposts":3}}}""",
                    Encoding.UTF8,
                    "application/json")
            });
        });

        using var httpClient = new HttpClient(handler);
        var provider = new MetricoolPublishingProvider(
            new MetricoolOptions(
                true,
                "https://metricool.test/api/",
                "v1/plans/{externalProfileId}/posts",
                "v1/posts/{externalPostId}/analytics",
                string.Empty,
                string.Empty,
                "access_token"),
            httpClient);

        var result = await provider.ReconcileAsync(
            new PublishingReconciliationRequest(
                "tenant_001",
                "brand_123",
                "token_123",
                "Instagram",
                "metricool_post_123",
                string.Empty),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("published", result.ProviderStatus);
        Assert.Equal("https://instagram.com/p/metricool-post", result.ExternalUrl);
        Assert.Equal(3200, result.Metrics.Reach);
        Assert.Equal(44, result.Metrics.Clicks);
        Assert.Equal(21, result.Metrics.Likes);
        Assert.Equal(4, result.Metrics.Comments);
        Assert.Equal(3, result.Metrics.Shares);
        Assert.Equal(new DateTime(2026, 03, 27, 18, 30, 00, DateTimeKind.Utc), result.PublishedUtc);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("https://metricool.test/api/v1/posts/metricool_post_123/analytics?access_token=token_123", capturedRequest.RequestUri!.ToString());
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}
