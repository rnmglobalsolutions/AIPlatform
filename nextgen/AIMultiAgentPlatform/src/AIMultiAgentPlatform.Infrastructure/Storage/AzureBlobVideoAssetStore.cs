using System.Net.Http;
using AIMultiAgentPlatform.Application.Abstractions.Video;
using AIMultiAgentPlatform.Infrastructure.Configuration;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AIMultiAgentPlatform.Infrastructure.Storage;

public sealed class AzureBlobVideoAssetStore : IVideoAssetStore, IDisposable
{
    private readonly BlobStorageOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public AzureBlobVideoAssetStore(BlobStorageOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
    }

    public async Task<VideoAssetStorageResult> StoreAsync(VideoAssetStorageRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return VideoAssetStorageResult.Failure("AzureBlobVideoAssetStore", "Blob video asset storage is disabled.");
        }

        if (!_options.HasRequiredConfiguration)
        {
            return VideoAssetStorageResult.Failure("AzureBlobVideoAssetStore", "Blob storage connection string is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderVideoUrl))
        {
            return VideoAssetStorageResult.Failure("AzureBlobVideoAssetStore", "Provider video URL is required.");
        }

        try
        {
            using var response = await _httpClient.GetAsync(request.ProviderVideoUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return VideoAssetStorageResult.Failure(
                    "AzureBlobVideoAssetStore",
                    $"Provider video download failed with {(int)response.StatusCode}: {Truncate(body, 300)}");
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(BuildBlobName(request));
            await blobClient.UploadAsync(
                contentStream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "video/mp4"
                    }
                },
                cancellationToken);

            return VideoAssetStorageResult.Success(blobClient.Uri.ToString(), "AzureBlobVideoAssetStore");
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or UriFormatException)
        {
            return VideoAssetStorageResult.Failure("AzureBlobVideoAssetStore", exception.Message);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string BuildBlobName(VideoAssetStorageRequest request)
    {
        var fileName = string.IsNullOrWhiteSpace(request.SuggestedFileName)
            ? $"{request.VideoGenerationJobId}.mp4"
            : request.SuggestedFileName.Trim();

        return $"{request.TenantId}/{request.DailyContentRequestId}/{request.VideoGenerationJobId}/{fileName}";
    }

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
}
