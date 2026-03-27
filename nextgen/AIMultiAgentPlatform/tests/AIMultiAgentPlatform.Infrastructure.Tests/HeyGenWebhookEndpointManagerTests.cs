using System.Net;
using System.Net.Http;
using System.Text;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using AIMultiAgentPlatform.Infrastructure.Video.HeyGen;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class HeyGenWebhookEndpointManagerTests
{
    [Fact]
    public async Task ListAsync_ParsesEndpoints()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "code": 100,
                          "data": [
                            {
                              "endpoint_id": "endpoint_123",
                              "url": "https://api.test/heygen/webhook",
                              "status": "enabled",
                              "events": ["avatar_video.success"],
                              "secret": "secret_123",
                              "created_at": "2026-03-26T12:00:00Z"
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                }));

        using var httpClient = new HttpClient(handler);
        var manager = new HeyGenWebhookEndpointManager(
            new HeyGenOptions(true, "test-key", "https://api.heygen.com/", string.Empty, string.Empty, 30),
            httpClient);

        var result = await manager.ListAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(result.Endpoints);
        Assert.Equal("endpoint_123", result.Endpoints[0].EndpointId);
        Assert.Equal("secret_123", result.Endpoints[0].Secret);
    }

    [Fact]
    public async Task CreateAsync_ParsesCreatedEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            _ = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "code": 100,
                      "data": {
                        "endpoint_id": "endpoint_456",
                        "url": "https://api.test/heygen/webhook",
                        "status": "enabled",
                        "events": ["avatar_video.success","avatar_video.failed"],
                        "secret": "secret_456",
                        "created_at": "2026-03-26T12:00:00Z"
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var manager = new HeyGenWebhookEndpointManager(
            new HeyGenOptions(true, "test-key", "https://api.heygen.com/", string.Empty, string.Empty, 30),
            httpClient);

        var result = await manager.CreateAsync(
            "https://api.test/heygen/webhook",
            ["avatar_video.success", "avatar_video.failed"],
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("endpoint_456", result.Endpoint!.EndpointId);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.heygen.com/v1/webhook/endpoint.add", capturedRequest.RequestUri!.ToString());
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}
