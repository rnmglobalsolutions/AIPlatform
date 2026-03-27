using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace AIMultiAgentPlatform.Infrastructure.Persistence.TableStorage;

internal abstract class TableStorageJsonRepositoryBase
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly TableClient _tableClient;

    protected TableStorageJsonRepositoryBase(TableClient tableClient)
    {
        _tableClient = tableClient;
    }

    protected abstract string PartitionKey { get; }

    protected async Task SaveDocumentAsync<TDocument>(string rowKey, TDocument document, CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        var entity = new TableEntity(PartitionKey, rowKey)
        {
            ["PayloadJson"] = JsonSerializer.Serialize(document, SerializerOptions)
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    protected async Task<TDocument?> FindDocumentAsync<TDocument>(string rowKey, CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);

        NullableResponse<TableEntity> response =
            await _tableClient.GetEntityIfExistsAsync<TableEntity>(PartitionKey, rowKey, cancellationToken: cancellationToken);

        if (!response.HasValue)
        {
            return default;
        }

        var entity = response.Value!;
        if (!entity.TryGetValue("PayloadJson", out var payloadValue) || payloadValue is not string payloadJson)
        {
            return default;
        }

        var document = JsonSerializer.Deserialize<TDocument>(payloadJson, SerializerOptions);
        return document;
    }

    protected async Task DeleteDocumentAsync(string rowKey, CancellationToken cancellationToken)
    {
        await _tableClient.CreateIfNotExistsAsync(cancellationToken);
        try
        {
            await _tableClient.DeleteEntityAsync(PartitionKey, rowKey, ETag.All, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
        }
    }
}
