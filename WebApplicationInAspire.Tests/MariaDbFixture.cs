using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using WebApplicationInAspire.DataAccess;

namespace WebApplicationInAspire.Tests;

/// <summary>Krever en separat testserver. Oppretter og sletter bare sin egen is202_test_*-database.</summary>
public sealed class MariaDbFixture : IAsyncLifetime
{
    public PortalFactory Factory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = "";
    private string adminConnection = "";
    private string databaseName = "";

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var connection = new MySqlConnectionStringBuilder(configured);
        if (!connection.Database.StartsWith("is202_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Bruk bare en separat testserver med Database=is202_test.");
        connection.Database = "";
        adminConnection = connection.ConnectionString;
        databaseName = "is202_test_" + Guid.NewGuid().ToString("N");
        await using (var sql = new MySqlConnection(adminConnection))
        {
            await sql.OpenAsync();
            await new MySqlCommand($"CREATE DATABASE `{databaseName}`", sql).ExecuteNonQueryAsync();
        }
        connection.Database = databaseName;
        ConnectionString = connection.ConnectionString;
        Factory = new PortalFactory(ConnectionString);
        // Starter den vanlige middleware-/Identity-/migrasjonsflyten.
        using var client = Factory.CreateClient();
        (await client.GetAsync("/")).EnsureSuccessStatusCode();
    }

    public ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseMySql(ConnectionString, new MariaDbServerVersion(new Version(11, 4, 0))).Options);

    public async Task<string> UserAsync(string role)
    {
        using var scope = Factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = $"{Guid.NewGuid():N}@example.test", Email = $"{Guid.NewGuid():N}@example.test" };
        var created = await manager.CreateAsync(user, PortalHttp.Password);
        Assert.True(created.Succeeded, string.Join(",", created.Errors.Select(error => error.Description)));
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        return user.UserName;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        MySqlConnection.ClearAllPools();
        if (databaseName.Length == 0) return;
        await using var sql = new MySqlConnection(adminConnection);
        await sql.OpenAsync();
        await new MySqlCommand($"DROP DATABASE `{databaseName}`", sql).ExecuteNonQueryAsync();
    }
}

public sealed class PortalFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Database:ApplyMigrationsOnStartup"] = "true",
            ["SeedUsers:PublicUser:Email"] = "", ["SeedUsers:Admin:Email"] = "",
            ["Logging:LogLevel:Default"] = "Warning"
        }));
    }
}

[CollectionDefinition("MariaDB")]
public sealed class MariaDbCollection : ICollectionFixture<MariaDbFixture> { }

public sealed class MariaDbFactAttribute : FactAttribute
{
    public MariaDbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING")))
            Skip = "Sett TEST_DB_CONNECTION_STRING til en separat MariaDB-testserver for å kjøre denne testen.";
    }
}
