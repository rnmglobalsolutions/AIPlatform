namespace AIMultiAgentPlatform.Application.Abstractions;

public interface IIdGenerator
{
    string NewId(string prefix);
}
