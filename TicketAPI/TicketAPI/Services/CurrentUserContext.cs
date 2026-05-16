using System.Security.Claims;

namespace TicketAPI.Services;

public sealed class CurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    public ClaimsPrincipal Principal => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public string Email => Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public Guid UserId
    {
        get
        {
            var raw = Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }

    public string AppCode => Principal.FindFirstValue("app_code") ?? string.Empty;

    public bool IsSuperAdmin => Principal.IsInRole("SuperAdmin");
}
