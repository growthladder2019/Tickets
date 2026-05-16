namespace TicketAPI.Contracts;

public sealed record RegisterRequest(string AppCode, string Email, string Password, string DisplayName);
public sealed record LoginRequest(Guid? SiteId, string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresUtc,
    string Email,
    string DisplayName,
    string AppCode,
    bool IsSuperAdmin);
