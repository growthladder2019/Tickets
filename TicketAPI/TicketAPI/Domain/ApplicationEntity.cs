namespace TicketAPI.Domain;

public sealed class ApplicationEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ApplicationUserLink> UserLinks { get; set; } = new List<ApplicationUserLink>();
    public ICollection<TicketEntity> Tickets { get; set; } = new List<TicketEntity>();
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = new List<RefreshTokenEntity>();
}
