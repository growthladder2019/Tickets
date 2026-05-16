namespace TicketAPI.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TicketSystem";
    public string Audience { get; set; } = "TicketSystem.Client";
    public string SigningKey { get; set; } = "please-change-this-signing-key-in-production";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
}
