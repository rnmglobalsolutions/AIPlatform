namespace AIMultiAgentPlatform.Domain.Content;

public sealed record RepurposeDirective(
    string Format,
    string Intent,
    string Prompt);
