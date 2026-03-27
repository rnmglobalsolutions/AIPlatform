using System.Net;
using System.Text;
using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Publishing.Buffer;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class BufferPublishingProviderTests
{
    [Fact]
    public async Task PublishAsync_PostsCreateUpdateRequest()
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
                    """{"success":true,"updates":[{"id":"buffer_update_123","service_update_id":"service_123","status":"buffer"}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var provider = new BufferPublishingProvider(new BufferOptions(true, "https://api.bufferapp.com/1/"), httpClient);

        var result = await provider.PublishAsync(
            new PublishingRequest(
                "tenant_001",
                "corr_001",
                "profile_123",
                "token_123",
                "Instagram",
                "Caption copy",
                "https://blob.test/video.mp4",
                new DateTime(2026, 03, 27, 18, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("service_123", result.ExternalPostId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.bufferapp.com/1/updates/create.json", capturedRequest.RequestUri!.ToString());
        Assert.Contains("access_token=token_123", capturedBody, StringComparison.Ordinal);
        Assert.Contains("profile_ids%5B%5D=profile_123", capturedBody, StringComparison.Ordinal);
        Assert.Contains("media%5Bphoto%5D=https%3A%2F%2Fblob.test%2Fvideo.mp4", capturedBody, StringComparison.Ordinal);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}
