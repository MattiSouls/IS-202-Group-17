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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity må konfigurere sine tabeller for de egne entitetene legges til.
        base.OnModelCreating(builder);

        builder.Entity<Resource>(resource =>
        {
            resource.ToTable("Resources");
            resource.Property(item => item.OwnerId).HasMaxLength(255);
            resource.Property(item => item.CreatedAtUtc).HasColumnType("datetime(6)");
            resource.HasOne(item => item.Owner)
                .WithMany()
                .HasForeignKey(item => item.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Need>(need =>
        {
            need.ToTable("Needs");
            need.Property(item => item.NeededAt).HasColumnType("datetime(6)");
            need.Property(item => item.CreatedByUserId).HasMaxLength(255);
            need.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
