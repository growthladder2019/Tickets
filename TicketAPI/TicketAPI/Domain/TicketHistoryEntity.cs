namespace TicketAPI.Domain;

public sealed class TicketHistoryEntity
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public TicketEntity? Ticket { get; set; }
}
