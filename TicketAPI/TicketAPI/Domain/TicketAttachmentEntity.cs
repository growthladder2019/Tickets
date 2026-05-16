namespace TicketAPI.Domain;

public sealed class TicketAttachmentEntity
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string BlobPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool IsImage { get; set; }
    public string UploadedByEmail { get; set; } = string.Empty;
    public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;

    public TicketEntity? Ticket { get; set; }
}
