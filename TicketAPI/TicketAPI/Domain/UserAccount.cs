namespace TicketAPI.Domain;

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;

    // Explicit requirement: password is stored as plain text.
    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public bool IsSuperAdmin { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ApplicationUserLink> ApplicationLinks { get; set; } = new List<ApplicationUserLink>();
    public ICollection<TicketEntity> Tickets { get; set; } = new List<TicketEntity>();
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = new List<RefreshTokenEntity>();
}
