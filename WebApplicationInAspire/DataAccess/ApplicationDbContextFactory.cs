using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebApplicationInAspire.DataAccess;

/// <summary>Lar EF-verktøyet lage migrasjoner uten a starte webapplikasjonen.</summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<ApplicationDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Standardverdien inneholder ikke passord og er kun for generering av migrasjoner.
        // database update krever en virkelig connection string via User Secrets eller miljo.
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=127.0.0.1;Port=3307;Database=resourcesdb;User ID=resourcesapp;";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 4, 0)))
            .Options;

        return new ApplicationDbContext(options);
    }
}
