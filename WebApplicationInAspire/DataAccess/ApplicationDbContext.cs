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
    }
}
