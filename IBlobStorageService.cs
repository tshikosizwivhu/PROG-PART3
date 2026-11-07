namespace RaceDay.Api.Services;

/// <summary>Stores uploaded images in cloud storage and returns their public URL.</summary>
public interface IBlobStorageService
{
    /// <param name="content">The image bytes.</param>
    /// <param name="contentType">For example image/png.</param>
    /// <param name="folder">Virtual folder inside the container, e.g. "banners" or "profiles".</param>
    /// <param name="extension">File extension including the dot, e.g. ".png".</param>
    /// <returns>The public URL of the stored image.</returns>
    Task<string> UploadImageAsync(Stream content, string contentType, string folder, string extension, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when no Azure Blob Storage connection string has been configured.</summary>
public class BlobStorageNotConfiguredException : Exception
{
    public BlobStorageNotConfiguredException()
        : base("Azure Blob Storage is not configured. Set AzureBlob:ConnectionString.") { }
}

public class AzureBlobOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "raceday-images";
}
