namespace TicketAPI.Domain;

public sealed class TicketEntity
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public Guid ApplicationId { get; set; }
    public Guid UserAccountId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public bool IsDeleted { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public ApplicationEntity? Application { get; set; }
    public UserAccount? UserAccount { get; set; }
    public UserAccount? AssignedToUser { get; set; }
    public ICollection<TicketHistoryEntity> History { get; set; } = new List<TicketHistoryEntity>();
    public ICollection<TicketAttachmentEntity> Attachments { get; set; } = new List<TicketAttachmentEntity>();
}
