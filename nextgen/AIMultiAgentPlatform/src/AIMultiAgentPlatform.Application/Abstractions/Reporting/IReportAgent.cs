using AIMultiAgentPlatform.Application.Reporting;

namespace AIMultiAgentPlatform.Application.Abstractions.Reporting;

public interface IReportAgent
{
    Task<ReportAgentResult> GenerateAsync(ReportGenerationContext context, CancellationToken cancellationToken);
}
