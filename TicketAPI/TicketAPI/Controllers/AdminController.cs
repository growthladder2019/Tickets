using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Data;
using TicketAPI.Domain;

namespace TicketAPI.Controllers;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/admin")]
public sealed class AdminController(TicketSystemDbContext dbContext) : ControllerBase
{
    [HttpGet("applications")]
    public async Task<ActionResult<IReadOnlyCollection<ApplicationAdminDto>>> ListApplications(CancellationToken cancellationToken)
    {
        var apps = await dbContext.Applications
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ApplicationAdminDto(x.Id, x.Code, x.Name, x.CreatedUtc))
            .ToListAsync(cancellationToken);

        return Ok(apps);
    }

    [HttpPost("applications")]
    public async Task<ActionResult<ApplicationAdminDto>> CreateApplication(CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var app = new ApplicationEntity
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            CreatedUtc = DateTime.UtcNow
        };

        dbContext.Applications.Add(app);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApplicationAdminDto(app.Id, app.Code, app.Name, app.CreatedUtc));
    }

    [HttpPut("applications/{applicationId:guid}")]
    public async Task<ActionResult<ApplicationAdminDto>> UpdateApplication(Guid applicationId, UpdateApplicationRequest request, CancellationToken cancellationToken)
    {
        var app = await dbContext.Applications.SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (app is null)
        {
            return NotFound();
        }

        app.Code = request.Code.Trim().ToUpperInvariant();
        app.Name = request.Name.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApplicationAdminDto(app.Id, app.Code, app.Name, app.CreatedUtc));
    }

    [HttpDelete("applications/{applicationId:guid}")]
    public async Task<IActionResult> DeleteApplication(Guid applicationId, CancellationToken cancellationToken)
    {
        var app = await dbContext.Applications.SingleOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        if (app is null)
        {
            return NotFound();
        }

        dbContext.Applications.Remove(app);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyCollection<UserAdminDto>>> ListUsers(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.ApplicationLinks)
            .ThenInclude(x => x.Application)
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);

        return Ok(users.Select(MapUserDto).ToList());
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserAdminDto>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return Conflict("User with this email already exists.");
        }

        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = request.Password,
            DisplayName = request.DisplayName.Trim(),
            IsSuperAdmin = request.IsSuperAdmin,
            CreatedUtc = DateTime.UtcNow
        };

        dbContext.Users.Add(user);

        var apps = await dbContext.Applications
            .Where(x => request.AppCodes.Contains(x.Code))
            .ToListAsync(cancellationToken);

        foreach (var app in apps)
        {
            dbContext.ApplicationUserLinks.Add(new ApplicationUserLink
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                UserAccountId = user.Id,
                CreatedUtc = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var withLinks = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.ApplicationLinks)
            .ThenInclude(x => x.Application)
            .SingleAsync(x => x.Id == user.Id, cancellationToken);

        return Ok(MapUserDto(withLinks));
    }

    [HttpPut("users/{userId:guid}")]
    public async Task<ActionResult<UserAdminDto>> UpdateUser(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.ApplicationLinks)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        user.DisplayName = request.DisplayName.Trim();
        user.IsSuperAdmin = request.IsSuperAdmin;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.Password = request.Password;
        }

        dbContext.ApplicationUserLinks.RemoveRange(user.ApplicationLinks);

        var apps = await dbContext.Applications
            .Where(x => request.AppCodes.Contains(x.Code))
            .ToListAsync(cancellationToken);

        foreach (var app in apps)
        {
            dbContext.ApplicationUserLinks.Add(new ApplicationUserLink
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                UserAccountId = user.Id,
                CreatedUtc = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var withLinks = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.ApplicationLinks)
            .ThenInclude(x => x.Application)
            .SingleAsync(x => x.Id == user.Id, cancellationToken);

        return Ok(MapUserDto(withLinks));
    }

    [HttpDelete("users/{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static UserAdminDto MapUserDto(UserAccount user)
    {
        var appCodes = user.ApplicationLinks
            .Select(x => x.Application?.Code ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        return new UserAdminDto(user.Id, user.Email, user.DisplayName, user.IsSuperAdmin, user.CreatedUtc, appCodes);
    }

    public sealed record ApplicationAdminDto(Guid Id, string Code, string Name, DateTime CreatedUtc);
    public sealed record CreateApplicationRequest(string Code, string Name);
    public sealed record UpdateApplicationRequest(string Code, string Name);

    public sealed record UserAdminDto(Guid Id, string Email, string DisplayName, bool IsSuperAdmin, DateTime CreatedUtc, IReadOnlyCollection<string> AppCodes);
    public sealed record CreateUserRequest(string Email, string Password, string DisplayName, bool IsSuperAdmin, IReadOnlyCollection<string> AppCodes);
    public sealed record UpdateUserRequest(string Password, string DisplayName, bool IsSuperAdmin, IReadOnlyCollection<string> AppCodes);
}
