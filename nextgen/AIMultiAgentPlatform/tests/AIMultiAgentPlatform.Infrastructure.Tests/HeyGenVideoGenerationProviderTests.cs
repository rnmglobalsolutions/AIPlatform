using System.Net;
using System.Net.Http;
using System.Text;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Video.HeyGen;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class HeyGenVideoGenerationProviderTests
{
    [Fact]
    public async Task SubmitAsync_SendsVideoAgentRequestAndParsesVideoId()
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
                    """{"error":null,"data":{"video_id":"video_123"}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var provider = new HeyGenVideoGenerationProvider(
            new HeyGenOptions(true, "test-key", "https://api.heygen.com/", string.Empty, "avatar_123", 30),
            httpClient);

        var result = await provider.SubmitAsync(
            new VideoGenerationRequest(
                "tenant_001",
                "corr-001",
                "default",
                "Launch Video",
                "HOOK\n\nBODY\n\nCTA",
                "English",
                "9:16"),
            CancellationToken.None);

        Assert.True(result.Submitted);
        Assert.Equal("video_123", result.ProviderJobId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.heygen.com/v1/video_agent/generate", capturedRequest.RequestUri!.ToString());
        Assert.True(capturedRequest.Headers.Contains("X-API-KEY"));
        Assert.NotNull(capturedBody);
        Assert.Contains("\"prompt\":", capturedBody!, StringComparison.Ordinal);
        Assert.Contains("\"avatar_id\":\"avatar_123\"", capturedBody!, StringComparison.Ordinal);
        Assert.Contains("\"orientation\":\"portrait\"", capturedBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStatusAsync_ParsesCompletedStatusPayload()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://heygen.test/captions.ass")
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "Dialogue: 0,0:00:00.00,0:00:02.00,Default,,0,0,0,,Hello world",
                            Encoding.UTF8,
                            "text/plain")
                    });
            }

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "code": 100,
                          "data": {
                            "id": "video_456",
                            "status": "completed",
                            "video_url": "https://heygen.test/final.mp4",
                            "caption_url": "https://heygen.test/captions.ass",
                            "error": null
                          },
                          "message": "Success"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                });
        });

        using var httpClient = new HttpClient(handler);
        var provider = new HeyGenVideoGenerationProvider(
            new HeyGenOptions(true, "test-key", "https://api.heygen.com/", string.Empty, string.Empty, 30),
            httpClient);

        var result = await provider.GetStatusAsync("video_456", CancellationToken.None);

        Assert.Equal("completed", result.Status);
        Assert.Equal("https://heygen.test/final.mp4", result.VideoDownloadUrl);
        Assert.Contains("Hello world", result.TranscriptText, StringComparison.Ordinal);
        Assert.NotNull(result.TranscriptSegments);
        Assert.Single(result.TranscriptSegments!);
        Assert.Equal(0d, result.TranscriptSegments![0].StartSeconds);
        Assert.Equal(2d, result.TranscriptSegments[0].EndSeconds);
        Assert.Equal(string.Empty, result.FailureReason);
    }

    [Fact]
    public async Task GetStatusAsync_ParsesFailureMessageFromErrorObject()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "code": 100,
                          "data": {
                            "id": "video_789",
                            "status": "failed",
                            "video_url": null,
                            "caption_url": null,
                            "error": {
                              "message": "Video is too long",
                              "detail": "Please upgrade your plan."
                            }
                          },
                          "message": "Success"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                }));

        using var httpClient = new HttpClient(handler);
        var provider = new HeyGenVideoGenerationProvider(
            new HeyGenOptions(true, "test-key", "https://api.heygen.com/", string.Empty, string.Empty, 30),
            httpClient);

        var result = await provider.GetStatusAsync("video_789", CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Contains("Video is too long", result.FailureReason, StringComparison.Ordinal);
        Assert.Contains("Please upgrade your plan.", result.FailureReason, StringComparison.Ordinal);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}
