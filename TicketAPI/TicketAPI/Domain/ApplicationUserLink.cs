namespace TicketAPI.Domain;

public sealed class ApplicationUserLink
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid UserAccountId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ApplicationEntity? Application { get; set; }
    public UserAccount? UserAccount { get; set; }
}
