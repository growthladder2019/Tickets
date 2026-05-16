using System.Security.Claims;

namespace TicketAPI.Services;

public interface ICurrentUserContext
{
    string Email { get; }
    Guid UserId { get; }
    string AppCode { get; }
    bool IsSuperAdmin { get; }
    ClaimsPrincipal Principal { get; }
}
