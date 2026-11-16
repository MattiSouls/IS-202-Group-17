using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using WebApplicationInAspire.Security;

namespace WebApplicationInAspire.Tests;

[Collection("MariaDB")]
public class ExpoDemoSeederTests(MariaDbFixture database)
{
    [MariaDbFact]
    public async Task Demo_is_opt_in_development_only_and_restart_preserves_edits()
    {
        var providerEmail = await database.UserAsync(RoleNames.ResourceProvider);
        var publicEmail = await database.UserAsync(RoleNames.PublicUser);
        await using var db = database.Context();
        var provider = await db.Users.SingleAsync(u => u.UserName == providerEmail);
        var publicUser = await db.Users.SingleAsync(u => u.UserName == publicEmail);
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DemoData:Enabled"] = "true", ["DemoData:Date"] = "2026-11-16",
            ["SeedUsers:Provider:Email"] = provider.Email, ["SeedUsers:PublicUser:Email"] = publicUser.Email
        }).Build();
        using var scope = database.Factory.Services.CreateScope();
        var environment = new HostingEnvironment { EnvironmentName = "Production" };
        await ExpoDemoSeeder.SeedAsync(scope.ServiceProvider, settings, environment);
        Assert.False(await db.Resources.AnyAsync(r => r.OwnerId == provider.Id));

        environment.EnvironmentName = "Development";
        settings["DemoData:Enabled"] = "false";
        await ExpoDemoSeeder.SeedAsync(scope.ServiceProvider, settings, environment);
        Assert.False(await db.Resources.AnyAsync(r => r.OwnerId == provider.Id));
        settings["DemoData:Enabled"] = "true";
        await ExpoDemoSeeder.SeedAsync(scope.ServiceProvider, settings, environment);
        var resources = await db.Resources.Where(r => r.OwnerId == provider.Id).ToListAsync();
        var needs = await db.Needs.Where(n => n.CreatedByUserId == publicUser.Id).ToListAsync();
        Assert.Equal(6, resources.Count); Assert.Equal(3, needs.Count);
        var pump = Assert.Single(needs, n => n.Type == "Pumpe");
        Assert.Equal("Urgent", pump.Priority);
        Assert.Equal(2, resources.Count(r => r.Type == pump.Type && r.Region == pump.Region && r.IsAvailable
            && r.AvailableFrom <= pump.NeededAt && r.AvailableUntil >= pump.NeededAt));
        resources[0].IsAvailable = false;
        resources[0].Name = "Redigert under demo";
        await db.SaveChangesAsync();
        await ExpoDemoSeeder.SeedAsync(scope.ServiceProvider, settings, environment);
        Assert.Equal(6, await db.Resources.CountAsync(r => r.OwnerId == provider.Id));
        Assert.Equal(3, await db.Needs.CountAsync(n => n.CreatedByUserId == publicUser.Id));
        await db.Entry(resources[0]).ReloadAsync();
        Assert.False(resources[0].IsAvailable); Assert.Equal("Redigert under demo", resources[0].Name);
    }
}
