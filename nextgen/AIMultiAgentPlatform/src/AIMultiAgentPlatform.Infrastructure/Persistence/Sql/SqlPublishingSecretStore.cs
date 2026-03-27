using AIMultiAgentPlatform.Application.Abstractions.Publishing;
using Microsoft.EntityFrameworkCore;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.Sql;

public sealed class SqlPublishingSecretStore : SqlAggregateDocumentRepositoryBase, IPublishingSecretStore
{
    private const string AggregateType = "PublishingAccessTokenSecret";

    public SqlPublishingSecretStore(IDbContextFactory<AiPlatformDbContext> dbContextFactory) : base(dbContextFactory)
    {
    }

    public Task SaveAccessTokenAsync(PublishingAccessTokenSecret secret, CancellationToken cancellationToken) =>
        SaveDocumentAsync(
            AggregateType,
            secret.SecretReference,
            secret.TenantId,
            secret,
            secret.CreatedUtc,
            lookupKey: Normalize(secret.ProviderName),
            lookupKey2: Normalize(secret.Platform).ToLowerInvariant(),
            sortUtc: secret.UpdatedUtc,
            cancellationToken: cancellationToken);

    public async Task<string?> GetAccessTokenAsync(string secretReference, CancellationToken cancellationToken)
    {
        var secret = await FindByIdAsync<PublishingAccessTokenSecret>(AggregateType, Normalize(secretReference), cancellationToken);
        return secret?.AccessToken;
    }
}
