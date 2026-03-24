namespace AIMultiAgentPlatform.Domain.Common;

public readonly record struct TenantId(string Value)
{
    public override string ToString() => Value;
}
