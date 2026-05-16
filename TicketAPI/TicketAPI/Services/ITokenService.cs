using TicketAPI.Domain;

namespace TicketAPI.Services;

public interface ITokenService
{
    (string AccessToken, DateTime ExpiresUtc) CreateAccessToken(UserAccount user, string appCode, bool isSuperAdmin);
    string CreateRefreshToken();
}
