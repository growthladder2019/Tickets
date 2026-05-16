namespace TicketAPI.Domain;

public sealed class RefreshTokenEntity
{
    public Guid Id { get; set; }
    public Guid UserAccountId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedUtc { get; set; }

    public UserAccount? UserAccount { get; set; }
    public ApplicationEntity? Application { get; set; }
}
