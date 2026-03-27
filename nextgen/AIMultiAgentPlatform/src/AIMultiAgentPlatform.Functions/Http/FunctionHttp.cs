using System.Net;
using System.Text.Json;
using AIMultiAgentPlatform.Application.Common;
using Microsoft.Azure.Functions.Worker.Http;

namespace AIMultiAgentPlatform.Functions.Http;

internal static class FunctionHttp
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T?> ReadJsonAsync<T>(HttpRequestData request, CancellationToken cancellationToken)
    {
        if (request.Body is null)
        {
            return default;
        }

        return await JsonSerializer.DeserializeAsync<T>(request.Body, JsonOptions, cancellationToken);
    }

    public static async Task<string> ReadBodyAsStringAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        if (request.Body is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(request.Body, leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public static T? DeserializeJson<T>(string payloadJson) =>
        string.IsNullOrWhiteSpace(payloadJson)
            ? default
            : JsonSerializer.Deserialize<T>(payloadJson, JsonOptions);

    public static async Task<HttpResponseData> CreatedAsync<T>(HttpRequestData request, T payload, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.Created);
        await WriteJsonAsync(response, payload, cancellationToken);
        return response;
    }

    public static async Task<HttpResponseData> AcceptedAsync<T>(HttpRequestData request, T payload, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.Accepted);
        await WriteJsonAsync(response, payload, cancellationToken);
        return response;
    }

    public static async Task<HttpResponseData> BadRequestAsync(
        HttpRequestData request,
        string title,
        Result<object?>? result,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await WriteJsonAsync(response, new
        {
            title,
            detail = result?.ErrorMessage,
            errorCode = result?.ErrorCode
        }, cancellationToken);
        return response;
    }

    public static async Task<HttpResponseData> UnauthorizedAsync(
        HttpRequestData request,
        string title,
        string detail,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.Unauthorized);
        await WriteJsonAsync(response, new { title, detail }, cancellationToken);
        return response;
    }

    public static Task<HttpResponseData> BadRequestAsync(
        HttpRequestData request,
        string title,
        string detail,
        CancellationToken cancellationToken) =>
        BadRequestPayloadAsync(request, title, detail, cancellationToken);

    private static async Task<HttpResponseData> BadRequestPayloadAsync(
        HttpRequestData request,
        string title,
        string detail,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await WriteJsonAsync(response, new { title, detail }, cancellationToken);
        return response;
    }

    private static async Task WriteJsonAsync<T>(HttpResponseData response, T payload, CancellationToken cancellationToken)
    {
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await JsonSerializer.SerializeAsync(response.Body, payload, JsonOptions, cancellationToken);
    }
}
