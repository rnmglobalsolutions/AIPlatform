using AIMultiAgentPlatform.Application.Abstractions.Reporting;
using AIMultiAgentPlatform.Application.Reporting;
using AIMultiAgentPlatform.Domain.Reporting;

namespace AIMultiAgentPlatform.Infrastructure.Reporting;

public sealed class HeuristicReportAgent : IReportAgent
{
    public Task<ReportAgentResult> GenerateAsync(ReportGenerationContext context, CancellationToken cancellationToken)
    {
        var snapshot = context.Snapshot;
        var source = context.Source;
        var latestSnapshots = (source.PublishedContentMetricSnapshots ?? Array.Empty<Domain.Publishing.PublishedContentMetricSnapshot>())
            .GroupBy(item => item.PublishedContentRecordId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.CapturedUtc).First())
            .ToArray();

        var topPlatform = ResolveTopPlatform(source, latestSnapshots);
        var executiveSummary =
            $"In {snapshot.MonthKey}, the system published {snapshot.PostsPublished} posts, drove {snapshot.TotalReach} reach, {snapshot.TotalClicks} clicks, {snapshot.AttributedLeads} attributed leads, and {snapshot.AttributedBookings} attributed bookings. " +
            $"Top performer: {snapshot.TopPerformingAssetTitle}. " +
            (string.IsNullOrWhiteSpace(topPlatform)
                ? "No clear platform winner emerged yet."
                : $"Best-performing platform this month was {topPlatform}.");

        var operationalSummary =
            $"Operations finished {snapshot.PostsPublished} published posts with {snapshot.ApprovedContentPackages} approvals and {snapshot.BlockedContentPackages} blocked packages. " +
            $"Average quality score was {snapshot.AverageQualityScore:F2}, reminders scheduled: {snapshot.ReminderTouchesScheduled}, follow-up touches scheduled: {snapshot.FollowUpTouchesScheduled}.";

        var recommendations = BuildRecommendations(snapshot, topPlatform);

        return Task.FromResult(new ReportAgentResult(executiveSummary, operationalSummary, recommendations));
    }

    private static string ResolveTopPlatform(
        MonthlyPerformanceSource source,
        IReadOnlyList<Domain.Publishing.PublishedContentMetricSnapshot> latestSnapshots)
    {
        var engagementByPlatform = source.PublishedContentRecords
            .Join(
                latestSnapshots,
                record => record.PublishedContentRecordId,
                snapshot => snapshot.PublishedContentRecordId,
                (record, snapshot) => new
                {
                    record.Platform,
                    Score = snapshot.Clicks + snapshot.Likes + snapshot.Comments + snapshot.Shares
                })
            .GroupBy(item => item.Platform, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Platform = group.Key,
                Score = group.Sum(item => item.Score)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Platform, StringComparer.Ordinal)
            .FirstOrDefault();

        if (engagementByPlatform is not null && engagementByPlatform.Score > 0)
        {
            return engagementByPlatform.Platform;
        }

        var attributedByPlatform = source.LeadProfiles
            .Where(item => !string.IsNullOrWhiteSpace(item.SourcePlatform))
            .GroupBy(item => item.SourcePlatform, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Platform = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Platform, StringComparer.Ordinal)
            .FirstOrDefault();

        return attributedByPlatform?.Platform ?? string.Empty;
    }

    private static IReadOnlyList<ReportRecommendation> BuildRecommendations(
        MonthlyPerformanceSnapshot snapshot,
        string topPlatform)
    {
        var recommendations = new List<ReportRecommendation>();

        if (snapshot.TotalClicks > 0 && snapshot.AttributedLeads == 0)
        {
            recommendations.Add(
                new ReportRecommendation(
                    "Tighten Lead Capture",
                    "High",
                    "Content is generating clicks but not converting those visits into attributed leads.",
                    "Review CTA destination, form friction, and ManyChat handoff tagging so every conversion path captures the originating post.",
                    $"{snapshot.TotalClicks} clicks with 0 attributed leads"));
        }

        if (snapshot.AttributedLeads > 0 && snapshot.AttributedBookings == 0)
        {
            recommendations.Add(
                new ReportRecommendation(
                    "Improve Booking Conversion",
                    "High",
                    "Leads are being captured, but they are not progressing into booked appointments.",
                    "Shorten the booking handoff, reinforce urgency in follow-up, and test a clearer booking CTA in the highest-performing content.",
                    $"{snapshot.AttributedLeads} attributed leads with 0 attributed bookings"));
        }

        if (!string.IsNullOrWhiteSpace(topPlatform))
        {
            recommendations.Add(
                new ReportRecommendation(
                    "Double Down on Winning Platform",
                    "Medium",
                    "One platform is already outperforming the rest based on reconciled metrics and attribution.",
                    $"Increase posting volume or repurpose priority for {topPlatform} next month, especially around the top-performing topic.",
                    $"Top platform: {topPlatform}"));
        }

        if (snapshot.BlockedContentPackages > 0)
        {
            recommendations.Add(
                new ReportRecommendation(
                    "Reduce Review Friction",
                    "Medium",
                    "A meaningful share of content packages required changes before approval.",
                    "Audit blocked packages and tighten the content brief or guardrails so more content ships on the first pass.",
                    $"{snapshot.BlockedContentPackages} blocked packages"));
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(
                new ReportRecommendation(
                    "Keep Current Momentum",
                    "Low",
                    "The month does not show a sharp operational bottleneck or attribution gap.",
                    "Maintain the current publishing cadence and continue reconciling metrics so the next cycle has more signal for optimization.",
                    $"{snapshot.PostsPublished} posts published"));
        }

        return recommendations
            .Take(3)
            .ToArray();
    }
}
