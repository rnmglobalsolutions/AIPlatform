using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Immutable;
using AIMultiAgentPlatform.Application.DependencyInjection;
using AIMultiAgentPlatform.Application.Intake;
using AIMultiAgentPlatform.Functions.Intake;
using AIMultiAgentPlatform.Infrastructure.DependencyInjection;
using AIMultiAgentPlatform.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIMultiAgentPlatform.IntegrationTests;

public sealed class TallyIntakeFunctionEndpointTests
{
    [Fact]
    public async Task PostAsync_WithValidSignature_ReturnsCreatedAndPersistsTenant()
    {
        var secret = "super-secret-key";
        var services = BuildServices();
        var function = CreateFunction(services, secret);
        var payload = """
        {
          "externalSubmissionId": "sub_http_001",
          "businessName": "RNM Growth",
          "primaryContactName": "Jane Doe",
          "primaryContactEmail": "jane@rnmgrowth.com",
          "niche": "Marketing consulting",
          "websiteUrl": "https://rnmgrowth.com",
          "instagramUrl": "https://www.instagram.com/rnmgrowth",
          "calendlyUrl": "https://calendly.com/rnm-growth/intro-call",
          "desiredAction": "Book a consultation from the content",
          "contentLanguage": "English"
        }
        """;

        var request = TestHttpRequestData.Create(
            payload,
            ComputeSignature(payload, secret));

        var response = await function.PostAsync(request, CancellationToken.None);
        var responseBody = await ReadResponseBodyAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(responseBody);
        var tenantId = json.RootElement.GetProperty("tenantId").GetString();
        Assert.StartsWith("tenant_", tenantId);

        var tenantRepository = services.GetRequiredService<AIMultiAgentPlatform.Application.Abstractions.Persistence.ITenantRepository>();
        var savedTenant = ((InMemoryTenantRepository)tenantRepository).Find(tenantId!);
        Assert.NotNull(savedTenant);
        Assert.Equal("https://rnmgrowth.com", savedTenant!.Profile.WebsiteUrl);
    }

    [Fact]
    public async Task PostAsync_WithInvalidSignature_ReturnsUnauthorized()
    {
        var services = BuildServices();
        var function = CreateFunction(services, "super-secret-key");
        var payload = """
        {
          "externalSubmissionId": "sub_http_002",
          "businessName": "RNM Growth",
          "primaryContactName": "Jane Doe",
          "primaryContactEmail": "jane@rnmgrowth.com",
          "niche": "Marketing consulting"
        }
        """;

        var request = TestHttpRequestData.Create(payload, "not-a-valid-signature");

        var response = await function.PostAsync(request, CancellationToken.None);
        var responseBody = await ReadResponseBodyAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("signature is invalid", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();
        return services.BuildServiceProvider();
    }

    private static TallyIntakeFunction CreateFunction(ServiceProvider services, string signingSecret)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TallyWebhook:SigningSecret"] = signingSecret
            })
            .Build();

        return new TallyIntakeFunction(
            services.GetRequiredService<ProcessTallySubmissionUseCase>(),
            services.GetRequiredService<EnqueueProcessTallySubmissionUseCase>(),
            configuration,
            NullLogger<TallyIntakeFunction>.Instance);
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class TestHttpRequestData : HttpRequestData
    {
        private TestHttpRequestData(FunctionContext functionContext, string body) : base(functionContext)
        {
            Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            Headers = new HttpHeadersCollection();
            Cookies = Array.Empty<IHttpCookie>();
            Url = new Uri("https://localhost/api/api/intake/tally");
            Identities = Array.Empty<ClaimsIdentity>();
            Method = "POST";
        }

        public override Stream Body { get; }
        public override HttpHeadersCollection Headers { get; }
        public override IReadOnlyCollection<IHttpCookie> Cookies { get; }
        public override Uri Url { get; }
        public override IEnumerable<ClaimsIdentity> Identities { get; }
        public override string Method { get; }

        public override HttpResponseData CreateResponse() => new TestHttpResponseData(FunctionContext);

        public static TestHttpRequestData Create(string body, string signature)
        {
            var request = new TestHttpRequestData(new TestFunctionContext(), body);
            request.Headers.Add("Content-Type", "application/json");
            request.Headers.Add("Tally-Signature", signature);
            return request;
        }
    }

    private sealed class TestHttpResponseData : HttpResponseData
    {
        public TestHttpResponseData(FunctionContext functionContext) : base(functionContext)
        {
            Headers = new HttpHeadersCollection();
            Body = new MemoryStream();
            Cookies = new TestHttpCookies();
        }

        public override HttpStatusCode StatusCode { get; set; }
        public override HttpHeadersCollection Headers { get; set; }
        public override Stream Body { get; set; }
        public override HttpCookies Cookies { get; }
    }

    private sealed class TestFunctionContext : FunctionContext
    {
        private IServiceProvider _instanceServices = new ServiceCollection().BuildServiceProvider();
        private IDictionary<object, object> _items = new Dictionary<object, object>();

        public override string InvocationId => Guid.NewGuid().ToString();
        public override string FunctionId => "TallyIntakePost";
        public override TraceContext TraceContext => new TestTraceContext();
        public override BindingContext BindingContext => new TestBindingContext();
        public override RetryContext RetryContext => new TestRetryContext();
        public override IServiceProvider InstanceServices { get => _instanceServices; set => _instanceServices = value; }
        public override FunctionDefinition FunctionDefinition => new TestFunctionDefinition();
        public override IDictionary<object, object> Items { get => _items; set => _items = value; }
        public override IInvocationFeatures Features { get; } = new TestInvocationFeatures();
        public override CancellationToken CancellationToken => CancellationToken.None;
    }

    private sealed class TestTraceContext : TraceContext
    {
        public override string TraceParent => string.Empty;
        public override string TraceState => string.Empty;
    }

    private sealed class TestRetryContext : RetryContext
    {
        public override int RetryCount => 0;
        public override int MaxRetryCount => 0;
    }

    private sealed class TestBindingContext : BindingContext
    {
        public override IReadOnlyDictionary<string, object?> BindingData { get; } = new Dictionary<string, object?>();
    }

    private sealed class TestFunctionDefinition : FunctionDefinition
    {
        public override string PathToAssembly => string.Empty;
        public override string EntryPoint => "TallyIntakePost";
        public override string Id => "TallyIntakePost";
        public override string Name => "TallyIntakePost";
        public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;
        public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;
        public override ImmutableArray<FunctionParameter> Parameters { get; } = ImmutableArray<FunctionParameter>.Empty;
    }

    private sealed class TestInvocationFeatures : IInvocationFeatures
    {
        private readonly Dictionary<Type, object> _features = new();

        public T? Get<T>() =>
            _features.TryGetValue(typeof(T), out var feature) ? (T)feature : default;

        public void Set<T>(T instance) => _features[typeof(T)] = instance!;

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class TestHttpCookies : HttpCookies
    {
        public override void Append(string name, string value) { }
        public override void Append(IHttpCookie cookie) { }
        public override IHttpCookie CreateNew() => new TestHttpCookie();
    }

    private sealed class TestHttpCookie : IHttpCookie
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTimeOffset? Expires { get; set; }
        public bool? HttpOnly { get; set; }
        public string? Domain { get; set; }
        public string? Path { get; set; }
        public SameSite SameSite { get; set; }
        public bool? Secure { get; set; }
        public double? MaxAge { get; set; }
    }
}
