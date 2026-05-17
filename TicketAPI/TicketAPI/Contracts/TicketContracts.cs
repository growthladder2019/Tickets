using TicketAPI.Domain;

namespace TicketAPI.Contracts;

public sealed record CreateTicketRequest(
    string RequesterName,
    string Title,
    string Description,
    string Message,
    string Category,
    string Priority,
    Guid? ApplicationId = null);

public sealed record UpdateTicketRequest(
    string Title,
    string Description,
    string Message,
    string Category,
    string Priority,
    TicketStatus Status);

public sealed record AddResponseRequest(string Message);
public sealed record AssignAgentRequest(Guid AgentUserId);

public sealed record TicketSummaryResponse(
    Guid Id,
    string TicketNumber,
    string Title,
    string Description,
    string Category,
    string Priority,
    string? AssignedAgentEmail,
    DateTime? AssignedAtUtc,
    TicketStatus Status,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record TicketHistoryResponse(
    Guid Id,
    string EventType,
    string Description,
    string ActorEmail,
    DateTime CreatedUtc);

public sealed record TicketAttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    bool IsImage,
    string BlobPath,
    string UploadedByEmail,
    DateTime UploadedUtc);

public sealed record TicketDetailResponse(
    Guid Id,
    string TicketNumber,
    string RequesterName,
    string Title,
    string Description,
    string Message,
    string Category,
    string Priority,
    string? AssignedAgentEmail,
    DateTime? AssignedAtUtc,
    TicketStatus Status,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    IReadOnlyCollection<TicketHistoryResponse> History,
    IReadOnlyCollection<TicketAttachmentResponse> Attachments);
