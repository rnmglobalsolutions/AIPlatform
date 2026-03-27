namespace AIMultiAgentPlatform.Application.Abstractions.Publishing;

public interface IPublishingProviderSelector
{
    IPublishingProvider? Resolve(string providerName);
}
