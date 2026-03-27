using AIMultiAgentPlatform.Application.Abstractions.Publishing;

namespace AIMultiAgentPlatform.Application.Publishing;

public sealed class PublishingProviderSelector(IEnumerable<IPublishingProvider> providers) : IPublishingProviderSelector
{
    private readonly IReadOnlyList<IPublishingProvider> _providers = providers.ToArray();

    public IPublishingProvider? Resolve(string providerName) =>
        _providers.FirstOrDefault(provider =>
            provider.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
}
