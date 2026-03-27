using System.Net.Http;
using System.Text;
using System.Text.Json;
using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Domain.Reporting;
using AIMultiAgentPlatform.Infrastructure.Configuration;

namespace AIMultiAgentPlatform.Infrastructure.Reporting;

public sealed class OpenAiReportAgent : IReportAgent, IDisposable
{
    private const string ReportSchema = """
        {
          "type": "object",
          "properties": {
            "executiveSummary": { "type": "string" },
            "operationalSummary": { "type": "string" },
            "recommendations": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "title": { "type": "string" },
                  "priority": { "type": "string" },
                  "rationale": { "type": "string" },
                  "recommendedAction": { "type": "string" },
                  "supportingMetric": { "type": "string" }
                },
                "required": [
                  "title",
                  "priority",
                  "rationale",
                  "recommendedAction",
                  "supportingMetric"
                ],
                "additionalProperties": false
              }
            }
          },
          "required": [
            "executiveSummary",
            "operationalSummary",
            "recommendations"
          ],
          "additionalProperties": false
        }
        """;

    private static readonly JsonElement ReportSchemaElement = JsonDocument.Parse(ReportSchema).RootElement.Clone();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly OpenAiOptions _options;
    private readonly HeuristicReportAgent _fallback;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public OpenAiReportAgent(OpenAiOptions options, HeuristicReportAgent fallback, HttpClient? httpClient = null)
    {
        _options = options;
        _fallback = fallback;
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = NormalizeBaseAddress(options.Endpoint);
        }
    }

    public async Task<ReportAgentResult> GenerateAsync(ReportGenerationContext context, CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.HasRequiredConfiguration)
        {
            return await _fallback.GenerateAsync(context, cancellationToken);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        httpRequest.Headers.Add("api-key", _options.ApiKey);
        httpRequest.Content = new StringContent(
            BuildRequestPayloadJson(context, _options.ContentModel, _options.ContentMaxOutputTokens),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await _fallback.GenerateAsync(context, cancellationToken);
            }

            using var document = JsonDocument.Parse(responseJson);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return await _fallback.GenerateAsync(context, cancellationToken);
            }

            using var contentDocument = JsonDocument.Parse(content);
            var root = contentDocument.RootElement;
            var executiveSummary = root.GetProperty("executiveSummary").GetString() ?? string.Empty;
            var operationalSummary = root.GetProperty("operationalSummary").GetString() ?? string.Empty;
            var recommendations = root.GetProperty("recommendations")
                .EnumerateArray()
                .Select(item => new ReportRecommendation(
                    item.GetProperty("title").GetString() ?? string.Empty,
                    item.GetProperty("priority").GetString() ?? string.Empty,
                    item.GetProperty("rationale").GetString() ?? string.Empty,
                    item.GetProperty("recommendedAction").GetString() ?? string.Empty,
                    item.GetProperty("supportingMetric").GetString() ?? string.Empty))
                .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                .ToArray();

            if (string.IsNullOrWhiteSpace(executiveSummary) || string.IsNullOrWhiteSpace(operationalSummary))
            {
                return await _fallback.GenerateAsync(context, cancellationToken);
            }

            return new ReportAgentResult(executiveSummary, operationalSummary, recommendations);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            return await _fallback.GenerateAsync(context, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string BuildRequestPayloadJson(ReportGenerationContext context, string model, int maxOutputTokens)
    {
        var payload = new
        {
            model,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a senior client-facing growth strategist and reporting advisor for a multi-tenant content, lead generation, and booking platform. Write reports that feel valuable to a paying client, not an internal engineer. Prioritize business outcomes, what content is actually working, where leads and bookings are coming from, and what the client should do over the next 30 days. Use only the provided data. If metric coverage is partial because of the connected publishing provider, say that briefly and then still give the strongest recommendation possible from the available evidence."
                },
                new
                {
                    role = "user",
                    content = BuildUserPrompt(context)
                }
            },
            max_completion_tokens = maxOutputTokens,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "monthly_report_bundle",
                    strict = true,
                    schema = ReportSchemaElement
                }
            }
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static string BuildUserPrompt(ReportGenerationContext context)
    {
        var snapshot = context.Snapshot;
        var source = context.Source;
        var latestMetricSnapshots = (source.PublishedContentMetricSnapshots ?? Array.Empty<Domain.Publishing.PublishedContentMetricSnapshot>())
            .GroupBy(item => item.PublishedContentRecordId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.CapturedUtc).First())
            .ToArray();
        var primaryAssetsByRequestId = source.PrimaryAssets
            .GroupBy(item => item.DailyContentRequestId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Headline,
                StringComparer.Ordinal);

        var platformLines = source.PublishedContentRecords
            .GroupBy(item => item.Platform, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var platform = group.Key;
                var recordIds = group.Select(item => item.PublishedContentRecordId).ToHashSet(StringComparer.Ordinal);
                var metrics = latestMetricSnapshots
                    .Where(item => recordIds.Contains(item.PublishedContentRecordId))
                    .ToArray();
                var attributedLeads = source.LeadProfiles.Count(item => string.Equals(item.SourcePlatform, platform, StringComparison.OrdinalIgnoreCase));
                var attributedBookings = source.BookingRecords.Count(item => string.Equals(item.AttributedPlatform, platform, StringComparison.OrdinalIgnoreCase));

                return $"- {platform}: posts={group.Count()}, reach={metrics.Sum(item => item.Reach)}, clicks={metrics.Sum(item => item.Clicks)}, leads={attributedLeads}, bookings={attributedBookings}";
            })
            .ToArray();

        var providerLines = source.PublishedContentRecords
            .GroupBy(item => item.ProviderName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var provider = group.Key;
                var recordIds = group.Select(item => item.PublishedContentRecordId).ToHashSet(StringComparer.Ordinal);
                var metrics = latestMetricSnapshots
                    .Where(item => recordIds.Contains(item.PublishedContentRecordId))
                    .ToArray();
                var postsWithMetrics = metrics.Length;
                var hasReach = metrics.Any(item => item.Reach > 0);
                var hasClicks = metrics.Any(item => item.Clicks > 0);
                var hasEngagement = metrics.Any(item => item.Likes > 0 || item.Comments > 0 || item.Shares > 0);
                var coverageSignals = new List<string>
                {
                    $"{postsWithMetrics}/{group.Count()} posts have reconciled metric snapshots"
                };

                if (hasReach)
                {
                    coverageSignals.Add("reach or impression data present");
                }

                if (hasClicks)
                {
                    coverageSignals.Add("click data present");
                }

                if (hasEngagement)
                {
                    coverageSignals.Add("engagement data present");
                }

                if (provider.Equals("Buffer", StringComparison.OrdinalIgnoreCase))
                {
                    coverageSignals.Add("network-native analytics may still be partial");
                }
                else if (provider.Equals("Metricool", StringComparison.OrdinalIgnoreCase))
                {
                    coverageSignals.Add("use this provider as a stronger cross-platform benchmark when coverage is broad");
                }
                else
                {
                    coverageSignals.Add("use only the provider-supplied metrics currently available");
                }

                return $"- {provider}: posts={group.Count()}, reach={metrics.Sum(item => item.Reach)}, clicks={metrics.Sum(item => item.Clicks)}. Coverage note: {string.Join("; ", coverageSignals)}.";
            })
            .ToArray();

        var topContentLines = source.PublishedContentRecords
            .Select(record =>
            {
                var metric = latestMetricSnapshots.FirstOrDefault(item => item.PublishedContentRecordId == record.PublishedContentRecordId);
                var title = primaryAssetsByRequestId.TryGetValue(record.DailyContentRequestId, out var headline)
                    ? headline
                    : record.Caption;
                var attributedLeads = source.LeadProfiles.Count(item => item.SourcePublishedContentRecordId == record.PublishedContentRecordId);
                var attributedBookings = source.BookingRecords.Count(item => item.AttributedPublishedContentRecordId == record.PublishedContentRecordId);
                var score = attributedBookings * 100L +
                            attributedLeads * 25L +
                            (metric?.Clicks ?? 0) +
                            (metric?.Likes ?? 0) +
                            (metric?.Comments ?? 0) +
                            (metric?.Shares ?? 0);

                return new
                {
                    record.ProviderName,
                    record.Platform,
                    Title = string.IsNullOrWhiteSpace(title) ? "Untitled post" : title,
                    Reach = metric?.Reach ?? 0,
                    Clicks = metric?.Clicks ?? 0,
                    Leads = attributedLeads,
                    Bookings = attributedBookings,
                    Score = score
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .Take(5)
            .Select(item => $"- {item.Title} [{item.Platform} via {item.ProviderName}]: reach={item.Reach}, clicks={item.Clicks}, leads={item.Leads}, bookings={item.Bookings}")
            .ToArray();

        var weakContentLines = source.PublishedContentRecords
            .Select(record =>
            {
                var metric = latestMetricSnapshots.FirstOrDefault(item => item.PublishedContentRecordId == record.PublishedContentRecordId);
                var title = primaryAssetsByRequestId.TryGetValue(record.DailyContentRequestId, out var headline)
                    ? headline
                    : record.Caption;
                var attributedLeads = source.LeadProfiles.Count(item => item.SourcePublishedContentRecordId == record.PublishedContentRecordId);
                var attributedBookings = source.BookingRecords.Count(item => item.AttributedPublishedContentRecordId == record.PublishedContentRecordId);
                var weaknessScore = (metric?.Reach ?? 0) - ((metric?.Clicks ?? 0) * 10) - (attributedLeads * 50) - (attributedBookings * 100);

                return new
                {
                    record.ProviderName,
                    record.Platform,
                    Title = string.IsNullOrWhiteSpace(title) ? "Untitled post" : title,
                    Reach = metric?.Reach ?? 0,
                    Clicks = metric?.Clicks ?? 0,
                    Leads = attributedLeads,
                    Bookings = attributedBookings,
                    WeaknessScore = weaknessScore
                };
            })
            .Where(item => item.Reach > 0)
            .OrderByDescending(item => item.WeaknessScore)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .Take(3)
            .Select(item => $"- {item.Title} [{item.Platform} via {item.ProviderName}]: reach={item.Reach}, clicks={item.Clicks}, leads={item.Leads}, bookings={item.Bookings}")
            .ToArray();

        return $$"""
            Client profile:
            - Business: {{context.Tenant.Profile.BusinessName}}
            - Niche: {{context.Tenant.Profile.Niche}}
            - Offer: {{context.Tenant.Profile.Offer}}
            - Target audience: {{context.Tenant.Profile.TargetAudience}}
            - Brand tone: {{context.Tenant.Profile.BrandTone}}
            - CTA keyword: {{context.Tenant.Profile.CallToActionKeyword}}
            - Desired action: {{context.Tenant.Profile.DesiredAction}}
            - Language: {{context.Tenant.Profile.ContentLanguage}}

            Reporting goal:
            - Explain what is actually creating business value.
            - Distinguish vanity metrics from business-driving results.
            - Give recommendations the client can act on next month.

            Month: {{snapshot.MonthKey}}
            Primary conversion action: {{snapshot.PrimaryConversionAction}}
            Top performing asset: {{snapshot.TopPerformingAssetTitle}}

            Snapshot metrics:
            - Posts published: {{snapshot.PostsPublished}}
            - Videos created: {{snapshot.VideosCreated}}
            - Graphics created: {{snapshot.GraphicsCreated}}
            - Average quality score: {{snapshot.AverageQualityScore}}
            - Leads generated: {{snapshot.LeadsGenerated}}
            - Marketing qualified leads: {{snapshot.MarketingQualifiedLeads}}
            - Booking ready leads: {{snapshot.BookingReadyLeads}}
            - Appointments booked: {{snapshot.AppointmentsBooked}}
            - Total reach: {{snapshot.TotalReach}}
            - Total clicks: {{snapshot.TotalClicks}}
            - Total likes: {{snapshot.TotalLikes}}
            - Total comments: {{snapshot.TotalComments}}
            - Total shares: {{snapshot.TotalShares}}
            - Attributed leads: {{snapshot.AttributedLeads}}
            - Attributed bookings: {{snapshot.AttributedBookings}}
            - Approved packages: {{snapshot.ApprovedContentPackages}}
            - Blocked packages: {{snapshot.BlockedContentPackages}}
            - Reminder touches scheduled: {{snapshot.ReminderTouchesScheduled}}
            - Follow-up touches scheduled: {{snapshot.FollowUpTouchesScheduled}}

            Provider coverage notes:
            {{string.Join("\n", providerLines)}}

            Platform breakdown:
            {{string.Join("\n", platformLines)}}

            Top content by business impact:
            {{string.Join("\n", topContentLines)}}

            Weak or under-converting content:
            {{string.Join("\n", weakContentLines)}}

            Return:
            1. A concise executive summary written for the client.
            2. A concise operational summary focused on what the team should keep, fix, or scale.
            3. Up to 3 practical recommendations with priority.
            4. Recommendations should mention a platform, content pattern, CTA, provider coverage note, or funnel stage whenever possible.
            """;
    }

    private static Uri NormalizeBaseAddress(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new Uri("https://invalid.local/openai/v1/", UriKind.Absolute);
        }

        var normalized = endpoint.Trim().TrimEnd('/');
        if (!normalized.Contains("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"{normalized}/openai/v1";
        }

        return new Uri($"{normalized.TrimEnd('/')}/", UriKind.Absolute);
    }
}
