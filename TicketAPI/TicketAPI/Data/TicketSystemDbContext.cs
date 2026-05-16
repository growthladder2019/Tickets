using Microsoft.EntityFrameworkCore;
using TicketAPI.Domain;

namespace TicketAPI.Data;

public sealed class TicketSystemDbContext(DbContextOptions<TicketSystemDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<ApplicationUserLink> ApplicationUserLinks => Set<ApplicationUserLink>();
    public DbSet<TicketEntity> Tickets => Set<TicketEntity>();
    public DbSet<TicketHistoryEntity> TicketHistory => Set<TicketHistoryEntity>();
    public DbSet<TicketAttachmentEntity> TicketAttachments => Set<TicketAttachmentEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("TicketSystem");

        modelBuilder.Entity<ApplicationEntity>(entity =>
        {
            entity.ToTable("Applications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Password).HasMaxLength(512).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IsSuperAdmin).HasDefaultValue(false);
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<ApplicationUserLink>(entity =>
        {
            entity.ToTable("ApplicationUsers");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ApplicationId, x.UserAccountId }).IsUnique();
            entity.HasOne(x => x.Application)
                .WithMany(x => x.UserLinks)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.UserAccount)
                .WithMany(x => x.ApplicationLinks)
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketEntity>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TicketNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RequesterName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Priority).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.TicketNumber).IsUnique();
            entity.HasIndex(x => new { x.ApplicationId, x.UserAccountId, x.Status, x.CreatedUtc });
            entity.HasIndex(x => x.AssignedToUserId);
            entity.HasOne(x => x.Application)
                .WithMany(x => x.Tickets)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UserAccount)
                .WithMany(x => x.Tickets)
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AssignedToUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TicketHistoryEntity>(entity =>
        {
            entity.ToTable("TicketHistory");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.ActorEmail).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => new { x.TicketId, x.CreatedUtc });
            entity.HasOne(x => x.Ticket)
                .WithMany(x => x.History)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketAttachmentEntity>(entity =>
        {
            entity.ToTable("TicketAttachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BlobPath).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
            entity.Property(x => x.UploadedByEmail).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => new { x.TicketId, x.UploadedUtc });
            entity.HasOne(x => x.Ticket)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Token).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasOne(x => x.UserAccount)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Application)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
