using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Services;

/// <summary>Uploads images to an Azure Blob Storage container (created on first use, public read access for blobs).</summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly AzureBlobOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private BlobContainerClient? _container;

    public AzureBlobStorageService(IOptions<AzureBlobOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> UploadImageAsync(Stream content, string contentType, string folder, string extension, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new BlobStorageNotConfiguredException();
        }

        var container = await GetContainerAsync(cancellationToken);
        var blob = container.GetBlobClient($"{folder}/{Guid.NewGuid():N}{extension}");

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };
        await blob.UploadAsync(content, uploadOptions, cancellationToken);

        return blob.Uri.ToString();
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container != null) return _container;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_container == null)
            {
                var container = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
                await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
                _container = container;
            }
            return _container;
        }
        finally
        {
            _lock.Release();
        }
    }
}
