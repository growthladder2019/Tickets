using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TicketAPI.Configuration;
using TicketAPI.Contracts;
using TicketAPI.Data;
using TicketAPI.Domain;
using TicketAPI.Services;

namespace TicketAPI.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    TicketSystemDbContext dbContext,
    ITokenService tokenService,
    IOptions<SuperAdminOptions> superAdminOptions) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var app = await dbContext.Applications.SingleOrDefaultAsync(x => x.Code == request.AppCode, cancellationToken);
        if (app is null)
        {
            return BadRequest("Invalid app code.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null)
        {
            user = new UserAccount
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = request.Password,
                DisplayName = request.DisplayName.Trim(),
                IsSuperAdmin = false,
                CreatedUtc = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
        }
        else if (user.Password != request.Password)
        {
            return Conflict("Email already exists with a different password.");
        }

        var linkExists = await dbContext.ApplicationUserLinks.AnyAsync(
            x => x.ApplicationId == app.Id && x.UserAccountId == user.Id,
            cancellationToken);
        if (!linkExists)
        {
            dbContext.ApplicationUserLinks.Add(new ApplicationUserLink
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                UserAccountId = user.Id,
                CreatedUtc = DateTime.UtcNow
            });
        }

        var (accessToken, expiresUtc) = tokenService.CreateAccessToken(user, app.Code, user.IsSuperAdmin);
        var refreshToken = tokenService.CreateRefreshToken();

        dbContext.RefreshTokens.Add(new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserAccountId = user.Id,
            ApplicationId = app.Id,
            Token = refreshToken,
            ExpiresUtc = DateTime.UtcNow.AddDays(7)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AuthResponse(accessToken, refreshToken, expiresUtc, user.Email, user.DisplayName, app.Code, user.IsSuperAdmin));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!request.SiteId.HasValue)
        {
            var superAdminEmail = request.Email.Trim().ToLowerInvariant();
            var adminUser = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == superAdminEmail && x.IsSuperAdmin, cancellationToken);

            if (adminUser is null || adminUser.Password != request.Password)
            {
                return Unauthorized("Invalid super admin credentials.");
            }

            var superAdmin = superAdminOptions.Value;
            var (adminAccessToken, adminExpiresUtc) = tokenService.CreateAccessToken(adminUser, superAdmin.AppCode, true);
            var adminRefreshToken = tokenService.CreateRefreshToken();

            dbContext.RefreshTokens.Add(new RefreshTokenEntity
            {
                Id = Guid.NewGuid(),
                UserAccountId = adminUser.Id,
                ApplicationId = null,
                Token = adminRefreshToken,
                ExpiresUtc = DateTime.UtcNow.AddDays(7)
            });
            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new AuthResponse(
                adminAccessToken,
                adminRefreshToken,
                adminExpiresUtc,
                adminUser.Email,
                adminUser.DisplayName,
                superAdmin.AppCode,
                true));
        }

        var app = await dbContext.Applications.SingleOrDefaultAsync(x => x.Id == request.SiteId.Value, cancellationToken);
        if (app is null)
        {
            return Unauthorized("Site ID is not recognized.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(
            x => x.Email == email,
            cancellationToken);

        if (user is null || user.Password != request.Password)
        {
            return Unauthorized("Invalid credentials.");
        }

        var linked = await dbContext.ApplicationUserLinks.AnyAsync(
            x => x.ApplicationId == app.Id && x.UserAccountId == user.Id,
            cancellationToken);
        if (!linked)
        {
            return Unauthorized("Email is not linked to this application.");
        }

        var (accessToken, expiresUtc) = tokenService.CreateAccessToken(user, app.Code, user.IsSuperAdmin);
        var refreshToken = tokenService.CreateRefreshToken();

        dbContext.RefreshTokens.Add(new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserAccountId = user.Id,
            ApplicationId = app.Id,
            Token = refreshToken,
            ExpiresUtc = DateTime.UtcNow.AddDays(7)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AuthResponse(accessToken, refreshToken, expiresUtc, user.Email, user.DisplayName, app.Code, user.IsSuperAdmin));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var token = await dbContext.RefreshTokens
            .Include(x => x.UserAccount)
            .Include(x => x.Application)
            .SingleOrDefaultAsync(x => x.Token == request.RefreshToken && x.RevokedUtc == null, cancellationToken);

        if (token is null || token.ExpiresUtc <= DateTime.UtcNow || token.UserAccount is null)
        {
            return Unauthorized("Refresh token is invalid.");
        }

        token.RevokedUtc = DateTime.UtcNow;
        var newRefreshToken = tokenService.CreateRefreshToken();
        dbContext.RefreshTokens.Add(new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserAccountId = token.UserAccountId,
            ApplicationId = token.ApplicationId,
            Token = newRefreshToken,
            ExpiresUtc = DateTime.UtcNow.AddDays(7)
        });

        var superAdmin = superAdminOptions.Value;
        var appCode = token.Application?.Code ?? superAdmin.AppCode;
        var isSuperAdmin = token.UserAccount.IsSuperAdmin || token.ApplicationId is null;
        var (accessToken, expiresUtc) = tokenService.CreateAccessToken(token.UserAccount, appCode, isSuperAdmin);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AuthResponse(
            accessToken,
            newRefreshToken,
            expiresUtc,
            token.UserAccount.Email,
            token.UserAccount.DisplayName,
            appCode,
            isSuperAdmin));
    }
}
