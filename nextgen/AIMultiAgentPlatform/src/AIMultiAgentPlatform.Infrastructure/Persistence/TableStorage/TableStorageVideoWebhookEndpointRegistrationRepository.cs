using AIMultiAgentPlatform.Application.Abstractions.Persistence;
using AIMultiAgentPlatform.Domain.Content;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Azure.Data.Tables;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.TableStorage;

internal sealed class TableStorageVideoWebhookEndpointRegistrationRepository : TableStorageJsonRepositoryBase, IVideoWebhookEndpointRegistrationRepository
{
    public TableStorageVideoWebhookEndpointRegistrationRepository(TableServiceClient tableServiceClient, TableStorageOptions options)
        : base(tableServiceClient.GetTableClient(options.VideoWebhookEndpointRegistrationTableName))
    {
    }

    protected override string PartitionKey => "VideoWebhookEndpointRegistration";

    public Task SaveAsync(VideoWebhookEndpointRegistration registration, CancellationToken cancellationToken) =>
        SaveDocumentAsync(registration.ProviderName, registration, cancellationToken);

    public Task<VideoWebhookEndpointRegistration?> FindByProviderAsync(string providerName, CancellationToken cancellationToken) =>
        FindDocumentAsync<VideoWebhookEndpointRegistration>(providerName, cancellationToken);

    public Task DeleteAsync(string providerName, CancellationToken cancellationToken) =>
        DeleteDocumentAsync(providerName, cancellationToken);
}
