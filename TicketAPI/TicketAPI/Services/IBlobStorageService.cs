namespace TicketAPI.Services;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string appCode, Guid ticketId, IFormFile file, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken);
}
