using System.Net;
using System.Net.Http;
using System.Text;
using AIMultiAgentPlatform.Application.Abstractions.AI;
using AIMultiAgentPlatform.Infrastructure.AI.OpenAi;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Tests;

public sealed class OpenAiContentGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_SendsStructuredRequestAndParsesJsonPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            var responseJson =
                """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"primaryHook\":\"Hook\",\"hookVariants\":[{\"label\":\"primary\",\"text\":\"Hook\"}],\"coreMessage\":\"Core\",\"body\":\"Body\",\"payoff\":\"Payoff\",\"callToAction\":\"CTA\",\"engagementPrompt\":\"Ask something\",\"desiredActionPrompt\":\"Comment BOOK\",\"languageGuidance\":\"English\",\"languageFormatInstruction\":\"English-first phrasing\",\"productionNotes\":\"HeyGen-compatible\",\"repurposeDirectives\":[{\"format\":\"Carousel\",\"intent\":\"Teach it\",\"prompt\":\"Close with CTA\"}]}"
                      }
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 111,
                    "completion_tokens": 222
                  }
                }
                """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        using var httpClient = new HttpClient(handler);
        var generator = new OpenAiContentGenerator(
            new OpenAiOptions(
                true,
                "https://example.openai.azure.com",
                "test-key",
                "gpt-5-mini",
                "gpt-5-mini",
                4000,
                6000),
            httpClient);

        var result = await generator.GenerateAsync(
            new ContentGenerationRequest(
                "tenant_001",
                "corr-001",
                "canonical-content-frame-v1",
                string.Empty,
                "system instructions",
                "user instructions"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("gpt-5-mini", result.Model);
        Assert.Equal(111, result.InputTokens);
        Assert.Equal(222, result.OutputTokens);
        Assert.Contains("\"primaryHook\":\"Hook\"", result.GeneratedPayloadJson, StringComparison.Ordinal);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://example.openai.azure.com/openai/v1/chat/completions", capturedRequest!.RequestUri!.ToString());
        Assert.True(capturedRequest.Headers.Contains("api-key"));
        Assert.NotNull(capturedBody);
        Assert.Contains("\"canonical_content_frame\"", capturedBody!, StringComparison.Ordinal);
        Assert.Contains("\"json_schema\"", capturedBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsFailureWhenResponseIsUnsuccessful()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":\"bad request\"}", Encoding.UTF8, "application/json")
                }));
        using var httpClient = new HttpClient(handler);
        var generator = new OpenAiContentGenerator(
            new OpenAiOptions(
                true,
                "https://example.openai.azure.com/openai/v1/",
                "test-key",
                "gpt-5-mini",
                "gpt-5-mini",
                4000,
                6000),
            httpClient);

        var result = await generator.GenerateAsync(
            new ContentGenerationRequest("tenant_001", "corr-001", "canonical-content-frame-v1", string.Empty, "system", "user"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("400", result.FailureReason, StringComparison.Ordinal);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}
