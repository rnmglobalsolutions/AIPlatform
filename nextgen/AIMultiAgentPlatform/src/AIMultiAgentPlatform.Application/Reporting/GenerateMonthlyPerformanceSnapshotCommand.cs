using AIMultiAgentPlatform.Contracts.Reporting;

namespace AIMultiAgentPlatform.Application.Reporting;

public sealed record GenerateMonthlyPerformanceSnapshotCommand(
    GenerateMonthlyPerformanceSnapshotRequest Request,
    string CorrelationId = "");
