namespace TicketAPI.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TicketSystem";
    public string Audience { get; set; } = "TicketSystem.Client";
    public string SigningKey { get; set; } = "replace-with-a-strong-long-key-for-production";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
}
