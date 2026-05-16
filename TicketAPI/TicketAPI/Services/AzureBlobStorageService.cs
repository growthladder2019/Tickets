using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using TicketAPI.Configuration;

namespace TicketAPI.Services;

public sealed class AzureBlobStorageService(IOptions<BlobStorageOptions> options) : IBlobStorageService
{
    private readonly BlobStorageOptions _blob = options.Value;

    private BlobContainerClient CreateContainerClient()
    {
        if (string.IsNullOrWhiteSpace(_blob.ConnectionString))
        {
            throw new InvalidOperationException("BlobStorage:ConnectionString is not configured.");
        }

        return new BlobContainerClient(_blob.ConnectionString, _blob.ContainerName);
    }

    public async Task<string> UploadAsync(string appCode, Guid ticketId, IFormFile file, CancellationToken cancellationToken)
    {
        var containerClient = CreateContainerClient();
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var safeFileName = Path.GetFileName(file.FileName);
        var blobPath = $"{appCode}/{ticketId}/{Guid.NewGuid():N}-{safeFileName}";
        var blobClient = containerClient.GetBlobClient(blobPath);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType }, cancellationToken: cancellationToken);
        return blobPath;
    }

    public async Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken)
    {
        var containerClient = CreateContainerClient();
        var blobClient = containerClient.GetBlobClient(blobPath);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }
}
