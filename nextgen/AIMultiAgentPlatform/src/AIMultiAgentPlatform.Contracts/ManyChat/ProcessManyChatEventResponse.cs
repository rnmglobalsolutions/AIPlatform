namespace AIMultiAgentPlatform.Contracts.ManyChat;

public sealed record ProcessManyChatEventResponse(
    string LeadProfileId,
    string ManyChatContactStateId,
    string LeadLifecycleStage,
    string TriggeredFlow,
    IReadOnlyList<string> TagsToAdd,
    IReadOnlyDictionary<string, string> FieldsToUpsert);
