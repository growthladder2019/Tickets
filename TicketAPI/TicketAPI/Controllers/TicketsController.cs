using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Contracts;
using TicketAPI.Data;
using TicketAPI.Domain;
using TicketAPI.Services;

namespace TicketAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets")]
public sealed class TicketsController(
    TicketSystemDbContext dbContext,
    ICurrentUserContext currentUser,
    IBlobStorageService blobStorageService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TicketDetailResponse>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        ApplicationEntity? app;
        if (currentUser.IsSuperAdmin)
        {
            if (!request.ApplicationId.HasValue)
            {
                return BadRequest("ApplicationId is required for super-admin ticket creation.");
            }

            app = await dbContext.Applications
                .SingleOrDefaultAsync(x => x.Id == request.ApplicationId.Value, cancellationToken);

            if (app is null)
            {
                return BadRequest("Invalid application id.");
            }
        }
        else
        {
            app = await ResolveAppAsync(cancellationToken);
            if (app is null)
            {
                return Forbid();
            }
        }

        var ticket = new TicketEntity
        {
            Id = Guid.NewGuid(),
            TicketNumber = $"TKT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            ApplicationId = app.Id,
            UserAccountId = currentUser.UserId,
            RequesterName = request.RequesterName.Trim(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Message = request.Message.Trim(),
            Category = request.Category.Trim(),
            Priority = request.Priority.Trim(),
            Status = TicketStatus.Open,
            IsDeleted = false,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        dbContext.Tickets.Add(ticket);
        dbContext.TicketHistory.Add(new TicketHistoryEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            EventType = "CREATED",
            Description = currentUser.IsSuperAdmin
                ? "Ticket created by super admin."
                : "Ticket created by requester.",
            ActorEmail = currentUser.Email,
            CreatedUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildTicketDetailAsync(ticket.Id, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TicketSummaryResponse>>> List([FromQuery] Guid? applicationId, CancellationToken cancellationToken)
    {
        var app = currentUser.IsSuperAdmin ? null : await ResolveAppAsync(cancellationToken);
        if (!currentUser.IsSuperAdmin && app is null)
        {
            return Forbid();
        }

        if (currentUser.IsSuperAdmin && applicationId.HasValue)
        {
            var applicationExists = await dbContext.Applications.AnyAsync(x => x.Id == applicationId.Value, cancellationToken);
            if (!applicationExists)
            {
                return BadRequest("Invalid application id.");
            }
        }

        var appId = app?.Id ?? Guid.Empty;
        var selectedApplicationId = applicationId;

        var tickets = await dbContext.Tickets
            .Where(x => !x.IsDeleted &&
                (currentUser.IsSuperAdmin
                    ? (!selectedApplicationId.HasValue || x.ApplicationId == selectedApplicationId.Value)
                    : (x.UserAccountId == currentUser.UserId && x.ApplicationId == appId)))
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => new TicketSummaryResponse(
                x.Id,
                x.TicketNumber,
                x.Title,
                x.Description,
                x.Category,
                x.Priority,
                x.AssignedToUser != null ? x.AssignedToUser.Email : null,
                x.AssignedAtUtc,
                x.Status,
                x.CreatedUtc,
                x.UpdatedUtc))
            .ToListAsync(cancellationToken);

        return Ok(tickets);
    }

    [HttpGet("{ticketId:guid}")]
    public async Task<ActionResult<TicketDetailResponse>> Get(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await FindOwnedTicketAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        return Ok(await BuildTicketDetailAsync(ticketId, cancellationToken));
    }

    [HttpPut("{ticketId:guid}")]
    public async Task<ActionResult<TicketDetailResponse>> Update(Guid ticketId, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await FindOwnedTicketAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!IsValidStatusTransition(ticket.Status, request.Status, currentUser.IsSuperAdmin))
        {
            return BadRequest("Invalid status transition.");
        }

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.Message = request.Message.Trim();
        ticket.Category = request.Category.Trim();
        ticket.Priority = request.Priority.Trim();
        ticket.Status = request.Status;
        ticket.UpdatedUtc = DateTime.UtcNow;

        dbContext.TicketHistory.Add(new TicketHistoryEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            EventType = "UPDATED",
            Description = $"Ticket updated. Status is now {ticket.Status}.",
            ActorEmail = currentUser.Email,
            CreatedUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildTicketDetailAsync(ticketId, cancellationToken));
    }

    [HttpDelete("{ticketId:guid}")]
    public async Task<IActionResult> Delete(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await FindOwnedTicketAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        ticket.IsDeleted = true;
        ticket.UpdatedUtc = DateTime.UtcNow;

        dbContext.TicketHistory.Add(new TicketHistoryEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            EventType = "DELETED",
            Description = "Ticket soft deleted.",
            ActorEmail = currentUser.Email,
            CreatedUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{ticketId:guid}/close")]
    public async Task<ActionResult<TicketDetailResponse>> Close(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await FindOwnedTicketAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!IsValidStatusTransition(ticket.Status, TicketStatus.Closed, currentUser.IsSuperAdmin))
        {
            return BadRequest("Ticket cannot be directly closed from current status.");
        }

        ticket.Status = TicketStatus.Closed;
        ticket.UpdatedUtc = DateTime.UtcNow;

        dbContext.TicketHistory.Add(new TicketHistoryEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            EventType = "CLOSED",
            Description = "Ticket closed by requester.",
            ActorEmail = currentUser.Email,
            CreatedUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildTicketDetailAsync(ticketId, cancellationToken));
    }

    [HttpPost("{ticketId:guid}/assign")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<TicketDetailResponse>> AssignAgent(Guid ticketId, AssignAgentRequest request, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .SingleOrDefaultAsync(x => x.Id == ticketId && !x.IsDeleted, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        var agent = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == request.AgentUserId, cancellationToken);
        if (agent is null)
        {
            return BadRequest("Selected agent does not exist.");
        }

        var linkedToApp = await dbContext.ApplicationUserLinks.AnyAsync(
            x => x.UserAccountId == agent.Id && x.ApplicationId == ticket.ApplicationId,
            cancellationToken);

        if (!linkedToApp && !agent.IsSuperAdmin)
        {
            return BadRequest("Agent must be linked to the ticket application.");
        }

        var wasAssigned = ticket.AssignedToUserId.HasValue;
        ticket.AssignedToUserId = agent.Id;
        ticket.AssignedAtUtc = DateTime.UtcNow;
        ticket.UpdatedUtc = DateTime.UtcNow;

        dbContext.TicketHistory.Add(new TicketHistoryEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            EventType = wasAssigned ? "REASSIGNED" : "ASSIGNED",
            Description = wasAssigned
                ? $"Ticket reassigned to {agent.Email}."
                : $"Ticket assigned to {agent.Email}.",
            ActorEmail = currentUser.Email,
            CreatedUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildTicketDetailAsync(ticketId, cancellationToken));
    }

    [HttpPost("{ticketId:guid}/response")]
    public async Task<ActionResult<TicketDetailResponse>> AddResponse(Guid ticketId, AddResponseRequest request, CancellationToken cancellationToken)
    {
        var ticket = await FindTicketForConversationAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        dbContext.TicketHistory.Add(new TicketHistoryEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            EventType = "RESPONSE",
            Description = request.Message.Trim(),
            ActorEmail = currentUser.Email,
            CreatedUtc = DateTime.UtcNow
        });

        ticket.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildTicketDetailAsync(ticketId, cancellationToken));
    }

    [HttpPost("{ticketId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<TicketDetailResponse>> UploadAttachment(Guid ticketId, IFormFile file, CancellationToken cancellationToken)
    {
        var ticket = await FindTicketForConversationAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        if (file.Length == 0 || file.Length > 20_000_000)
        {
            return BadRequest("File size must be between 1 byte and 20 MB.");
        }

        var allowed = new[] { "image/png", "image/jpeg", "image/webp", "application/pdf", "text/plain" };
        if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Unsupported file type.");
        }

        var blobPath = await blobStorageService.UploadAsync(currentUser.AppCode, ticket.Id, file, cancellationToken);

        dbContext.TicketAttachments.Add(new TicketAttachmentEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            BlobPath = blobPath,
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            IsImage = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase),
            UploadedByEmail = currentUser.Email,
            UploadedUtc = DateTime.UtcNow
        });

        dbContext.TicketHistory.Add(new TicketHistoryEntity
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            EventType = "ATTACHMENT_UPLOADED",
            Description = $"Attachment uploaded: {Path.GetFileName(file.FileName)}",
            ActorEmail = currentUser.Email,
            CreatedUtc = DateTime.UtcNow
        });

        ticket.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await BuildTicketDetailAsync(ticketId, cancellationToken));
    }

    [HttpGet("{ticketId:guid}/attachments/{attachmentId:guid}/content")]
    public async Task<IActionResult> GetAttachmentContent(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var ticket = await FindTicketForConversationAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        var attachment = await dbContext.TicketAttachments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == attachmentId && x.TicketId == ticketId, cancellationToken);

        if (attachment is null)
        {
            return NotFound();
        }

        var stream = await blobStorageService.DownloadAsync(attachment.BlobPath, cancellationToken);
        return File(stream, attachment.ContentType, attachment.FileName);
    }

    private async Task<TicketEntity?> FindOwnedTicketAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var app = currentUser.IsSuperAdmin ? null : await ResolveAppAsync(cancellationToken);
        if (!currentUser.IsSuperAdmin && app is null)
        {
            return null;
        }

        return await dbContext.Tickets.SingleOrDefaultAsync(
            x => x.Id == ticketId && !x.IsDeleted && (currentUser.IsSuperAdmin || (x.UserAccountId == currentUser.UserId && x.ApplicationId == app!.Id)),
            cancellationToken);
    }

    private async Task<ApplicationEntity?> ResolveAppAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Applications.SingleOrDefaultAsync(x => x.Code == currentUser.AppCode, cancellationToken);
    }

    private async Task<TicketEntity?> FindTicketForConversationAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.SingleOrDefaultAsync(x => x.Id == ticketId && !x.IsDeleted, cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        if (currentUser.IsSuperAdmin || ticket.UserAccountId == currentUser.UserId)
        {
            return ticket;
        }

        if (!ticket.AssignedToUserId.HasValue)
        {
            return null;
        }

        var linkedToTicketApp = await dbContext.ApplicationUserLinks.AnyAsync(
            x => x.ApplicationId == ticket.ApplicationId && x.UserAccountId == currentUser.UserId,
            cancellationToken);

        return linkedToTicketApp ? ticket : null;
    }

    private async Task<TicketDetailResponse> BuildTicketDetailAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(x => x.Id == ticketId, cancellationToken);

        var assignedAgentEmail = ticket.AssignedToUserId.HasValue
            ? await dbContext.Users
                .AsNoTracking()
                .Where(x => x.Id == ticket.AssignedToUserId.Value)
                .Select(x => x.Email)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var history = await dbContext.TicketHistory
            .AsNoTracking()
            .Where(x => x.TicketId == ticketId)
            .OrderBy(x => x.CreatedUtc)
            .Select(x => new TicketHistoryResponse(x.Id, x.EventType, x.Description, x.ActorEmail, x.CreatedUtc))
            .ToListAsync(cancellationToken);

        var attachments = await dbContext.TicketAttachments
            .AsNoTracking()
            .Where(x => x.TicketId == ticketId)
            .OrderByDescending(x => x.UploadedUtc)
            .Select(x => new TicketAttachmentResponse(
                x.Id,
                x.FileName,
                x.ContentType,
                x.FileSizeBytes,
                x.IsImage,
                x.BlobPath,
                x.UploadedByEmail,
                x.UploadedUtc))
            .ToListAsync(cancellationToken);

        return new TicketDetailResponse(
            ticket.Id,
            ticket.TicketNumber,
            ticket.RequesterName,
            ticket.Title,
            ticket.Description,
            ticket.Message,
            ticket.Category,
            ticket.Priority,
            assignedAgentEmail,
            ticket.AssignedAtUtc,
            ticket.Status,
            ticket.CreatedUtc,
            ticket.UpdatedUtc,
            history,
            attachments);
    }

    private static bool IsValidStatusTransition(TicketStatus current, TicketStatus next, bool isSuperAdmin)
    {
        if (isSuperAdmin)
        {
            return true;
        }

        if (current == next)
        {
            return true;
        }

        return (current, next) switch
        {
            (TicketStatus.InProgress, TicketStatus.Open) => true,
            (TicketStatus.Resolved, TicketStatus.Open) => true,
            (TicketStatus.Open, TicketStatus.InProgress) => true,
            (TicketStatus.Open, TicketStatus.Resolved) => true,
            (TicketStatus.InProgress, TicketStatus.Resolved) => true,
            (TicketStatus.Resolved, TicketStatus.Closed) => true,
            (TicketStatus.Open, TicketStatus.Closed) => false,
            _ => false
        };
    }
}
