namespace AIMultiAgentPlatform.Application.Abstractions.AI;

public sealed record StrategyPlannerRequest(
    string TenantId,
    string CorrelationId,
    string PromptVersion,
    string ModelHint,
    string SystemContext,
    string UserPrompt,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record StrategyPlannerResult(
    bool Succeeded,
    string StrategyBlueprintJson,
    string Model,
    string PromptVersion,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    string FailureReason)
{
    public static StrategyPlannerResult Success(
        string strategyBlueprintJson,
        string model,
        string promptVersion,
        int inputTokens = 0,
        int outputTokens = 0,
        decimal estimatedCostUsd = 0m) =>
        new(
            true,
            strategyBlueprintJson,
            model,
            promptVersion,
            inputTokens,
            outputTokens,
            estimatedCostUsd,
            string.Empty);

    public static StrategyPlannerResult Failure(
        string promptVersion,
        string model,
        string failureReason) =>
        new(
            false,
            string.Empty,
            model,
            promptVersion,
            0,
            0,
            0m,
            failureReason);
}

public sealed record ContentGenerationRequest(
    string TenantId,
    string CorrelationId,
    string PromptVersion,
    string ModelHint,
    string SystemContext,
    string UserPrompt,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record ContentGenerationResult(
    bool Succeeded,
    string GeneratedPayloadJson,
    string Model,
    string PromptVersion,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    string FailureReason)
{
    public static ContentGenerationResult Success(
        string generatedPayloadJson,
        string model,
        string promptVersion,
        int inputTokens = 0,
        int outputTokens = 0,
        decimal estimatedCostUsd = 0m) =>
        new(
            true,
            generatedPayloadJson,
            model,
            promptVersion,
            inputTokens,
            outputTokens,
            estimatedCostUsd,
            string.Empty);

    public static ContentGenerationResult Failure(
        string promptVersion,
        string model,
        string failureReason) =>
        new(
            false,
            string.Empty,
            model,
            promptVersion,
            0,
            0,
            0m,
            failureReason);
}

public sealed record ContentGuardrailValidationRequest(
    string Workflow,
    string PayloadJson,
    IReadOnlyList<string>? BlockedTopics = null,
    IReadOnlyDictionary<string, string>? Constraints = null);

public sealed record GuardrailValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static GuardrailValidationResult Valid(IReadOnlyList<string>? warnings = null) =>
        new(true, Array.Empty<string>(), warnings ?? Array.Empty<string>());

    public static GuardrailValidationResult Invalid(
        IReadOnlyList<string> errors,
        IReadOnlyList<string>? warnings = null) =>
        new(false, errors, warnings ?? Array.Empty<string>());
}
