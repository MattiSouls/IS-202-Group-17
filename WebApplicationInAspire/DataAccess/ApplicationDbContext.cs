using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.DataAccess;

/// <summary>Felles MariaDB-kontekst for ressurser og Identity sine brukere og roller.</summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Need> Needs => Set<Need>();
    public DbSet<PortalNotification> Notifications => Set<PortalNotification>();
    public DbSet<AllocationEvent> AllocationEvents => Set<AllocationEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity må konfigurere sine tabeller for de egne entitetene legges til.
        base.OnModelCreating(builder);

        builder.Entity<Resource>(resource =>
        {
            resource.ToTable("Resources");
            resource.Property(item => item.Version).IsConcurrencyToken();
            resource.HasIndex(item => new { item.OwnerId, item.CreationToken }).IsUnique();
            resource.HasIndex(item => new { item.Region, item.Type });
            resource.HasIndex(item => new { item.Latitude, item.Longitude });
            resource.Property(item => item.OwnerId).HasMaxLength(255);
            resource.Property(item => item.CreatedAtUtc).HasColumnType("datetime(6)");
            resource.Property(item => item.AvailableFrom).HasColumnType("datetime(6)");
            resource.Property(item => item.AvailableUntil).HasColumnType("datetime(6)");
            // Ressurser registrert før tilgjengelighetsfeltet ble innført, er fortsatt tilgjengelige.
            resource.Property(item => item.IsAvailable).HasDefaultValue(true);
            resource.HasOne(item => item.Owner)
                .WithMany()
                .HasForeignKey(item => item.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AllocationEvent>(entry =>
        {
            entry.HasIndex(e => new { e.NeedId, e.Id });
            entry.HasOne(e => e.Need).WithMany().HasForeignKey(e => e.NeedId).OnDelete(DeleteBehavior.SetNull);
            entry.HasOne(e => e.Resource).WithMany().HasForeignKey(e => e.ResourceId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<PortalNotification>(notice =>
        {
            notice.HasIndex(item => new { item.UserId, item.ReadAtUtc, item.Id });
            notice.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            notice.HasOne(item => item.Need).WithMany().HasForeignKey(item => item.NeedId).OnDelete(DeleteBehavior.SetNull);
            notice.HasOne(item => item.Resource).WithMany().HasForeignKey(item => item.ResourceId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Need>(need =>
        {
            need.ToTable("Needs");
            need.Property(item => item.Version).IsConcurrencyToken();
            need.HasIndex(item => new { item.CreatedByUserId, item.CreationToken }).IsUnique();
            need.HasIndex(item => new { item.Status, item.NeededAt });
            need.Property(item => item.NeededAt).HasColumnType("datetime(6)");
            need.Property(item => item.CreatedByUserId).HasMaxLength(255);
            need.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            need.HasOne(item => item.AssignedResource)
                .WithMany()
                .HasForeignKey(item => item.AssignedResourceId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
