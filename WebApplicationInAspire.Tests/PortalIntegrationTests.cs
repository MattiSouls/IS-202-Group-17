using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.Models.Entities;
using WebApplicationInAspire.Services;

namespace WebApplicationInAspire.Tests;

[Collection("MariaDB")]
[Trait("Category", "Integration")]
public sealed class PortalIntegrationTests(MariaDbFixture database)
{
    private async Task<(string Email, PortalHttp Http)> Login(string role = "ResourceProvider")
    {
        var email = await database.UserAsync(role);
        var http = new PortalHttp(database.Factory); await http.LoginAsync(email);
        return (email, http);
    }
    private async Task<string> UserId(string email)
    {
        await using var db = database.Context();
        return (await db.Users.SingleAsync(user => user.UserName == email)).Id;
    }
    private async Task<Resource> AddResource(string owner, string? type = null)
    {
        await using var db = database.Context();
        var resource = new Resource
        {
            Name = "Pumpe " + Guid.NewGuid().ToString("N"), Type = type ?? "Pumpe", Region = "Agder", OwnerId = owner,
            ContactName = "Testperson", PhoneNumber = "+4799999999", Latitude = 58.1467, Longitude = 7.9956,
            AvailableFrom = new DateTime(2026, 11, 1), AvailableUntil = new DateTime(2026, 11, 30)
        };
        db.Resources.Add(resource); await db.SaveChangesAsync(); return resource;
    }
    private async Task<Need> AddNeed(string owner, string type = "Pumpe")
    {
        await using var db = database.Context();
        var need = new Need { Type = type, Region = "Agder", NeededAt = new DateTime(2026, 11, 6, 12, 0, 0),
            CreatedByUserId = owner, Priority = "High", ContactName = "Testperson", PhoneNumber = "+4799999999" };
        db.Needs.Add(need); await db.SaveChangesAsync(); return need;
    }
    private static void Denied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [MariaDbFact]
    public async Task Registration_stores_hashed_password_and_cannot_choose_admin_role()
    {
        using var http = new PortalHttp(database.Factory);
        var email = $"{Guid.NewGuid():N}@example.test";
        var response = await http.PostAsync("/Account/Register", new() { ["Email"] = email,
            ["Password"] = PortalHttp.Password, ["ConfirmPassword"] = PortalHttp.Password, ["Role"] = "Admin" }, "/Account/Register");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = database.Factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await manager.FindByEmailAsync(email); Assert.NotNull(user);
        Assert.NotEqual(PortalHttp.Password, user.PasswordHash);
        Assert.True(await manager.CheckPasswordAsync(user, PortalHttp.Password));
        Assert.Equal(["ResourceProvider"], await manager.GetRolesAsync(user));
    }

    [MariaDbFact]
    public async Task Resource_CRUD_preserves_owner_period_and_database_values()
    {
        var (email, http) = await Login(); using var session = http;
        var name = "CRUD " + Guid.NewGuid().ToString("N");
        var fields = PortalHttp.ResourceFields(name); fields["OwnerId"] = "forged";
        Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync("/Resource/Register", fields, "/Resource/Register")).StatusCode);
        await using var db = database.Context();
        var resource = await db.Resources.AsNoTracking().SingleAsync(row => row.Name == name);
        Assert.Equal(await UserId(email), resource.OwnerId);
        Assert.Equal(new DateTime(2026, 11, 1, 8, 0, 0), resource.AvailableFrom);
        fields["Id"] = resource.Id.ToString(); fields["Name"] = name + " endret"; fields["IsAvailable"] = "false";
        fields["AvailableUntil"] = "2026-11-20T18:00";
        Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync($"/Resource/Edit/{resource.Id}", fields, $"/Resource/Edit/{resource.Id}")).StatusCode);
        resource = await db.Resources.AsNoTracking().SingleAsync(row => row.Id == resource.Id);
        Assert.False(resource.IsAvailable); Assert.Equal(20, resource.AvailableUntil!.Value.Day);
        Assert.Contains(name + " endret", await http.Client.GetStringAsync("/Resource/Overview"));
        (await http.Client.GetAsync($"/Resource/Delete/{resource.Id}")).EnsureSuccessStatusCode();
        Assert.True(await db.Resources.AnyAsync(row => row.Id == resource.Id)); // GET sletter ikke.
        Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync($"/Resource/Delete/{resource.Id}", new(), $"/Resource/Delete/{resource.Id}")).StatusCode);
        Assert.False(await db.Resources.AnyAsync(row => row.Id == resource.Id));
    }

    [MariaDbFact]
    public async Task Invalid_period_and_coordinates_return_errors_without_insert()
    {
        var (_, http) = await Login(); using var session = http;
        foreach (var invalid in new[] { "reversed", "missing", "range", "year" })
        {
            var fields = PortalHttp.ResourceFields(Guid.NewGuid().ToString("N"));
            if (invalid == "reversed") fields["AvailableUntil"] = "2026-10-01T08:00";
            if (invalid == "missing") fields.Remove("AvailableFrom");
            if (invalid == "range") fields["Latitude"] = "100";
            if (invalid == "year") fields["AvailableFrom"] = "0001-01-01T00:00";
            var response = await http.PostAsync("/Resource/Register", fields, "/Resource/Register");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
            await using var db = database.Context(); Assert.False(await db.Resources.AnyAsync(row => row.Name == fields["Name"]));
        }
    }

    [MariaDbFact]
    public async Task Roles_ownership_CSRF_and_encoded_content_are_enforced_by_HTTP_pipeline()
    {
        using var anonymous = new PortalHttp(database.Factory);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.Client.GetAsync("/Resource/Overview")).StatusCode);
        var (email, owner) = await Login(); using var a = owner;
        var (_, stranger) = await Login(); using var b = stranger;
        var resource = await AddResource(await UserId(email));
        Denied(await stranger.Client.GetAsync($"/Resource/Edit/{resource.Id}"));
        // Bruk et lovlig skjema til å hente token; URL-eierskap kontrolleres ved POST.
        Denied(await stranger.PostAsync($"/Resource/Delete/{resource.Id}", new(), "/Resource/Register"));
        Denied(await owner.Client.GetAsync("/Need/Register"));
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Client.PostAsync("/Resource/Register",
            new FormUrlEncodedContent(PortalHttp.ResourceFields("No token")))).StatusCode);
        var malicious = "<script>alert('xss')</script>";
        var fields = PortalHttp.ResourceFields(malicious);
        Assert.Equal(HttpStatusCode.Redirect, (await owner.PostAsync("/Resource/Register", fields, "/Resource/Register")).StatusCode);
        var html = await owner.Client.GetStringAsync("/Resource/Overview");
        Assert.DoesNotContain(malicious, html); Assert.Contains("&lt;script&gt;", html);
        await using var db = database.Context(); Assert.True(await db.Resources.AnyAsync(row => row.Id == resource.Id));
    }

    [MariaDbFact]
    public async Task Public_and_regional_overview_hide_private_unavailable_and_legacy_periods()
    {
        var (email, owner) = await Login(); using var a = owner;
        var (_, publicUser) = await Login("PublicUser"); using var b = publicUser;
        var id = await UserId(email);
        var visible = await AddResource(id); var hidden = await AddResource(id); var legacy = await AddResource(id);
        await using (var db = database.Context())
        {
            await db.Resources.Where(row => row.Id == hidden.Id).ExecuteUpdateAsync(update => update.SetProperty(row => row.IsAvailable, false));
            await db.Resources.Where(row => row.Id == legacy.Id).ExecuteUpdateAsync(update => update.SetProperty(row => row.AvailableFrom, (DateTime?)null));
        }
        var publicHtml = await publicUser.Client.GetStringAsync("/Resource/Overview?region=Agder");
        Assert.Contains(visible.Name, publicHtml); Assert.DoesNotContain(hidden.Name, publicHtml); Assert.DoesNotContain(legacy.Name, publicHtml);
        var ownerHtml = await owner.Client.GetStringAsync("/Resource/Overview");
        Assert.Contains(hidden.Name, ownerHtml); Assert.Contains("Tidsrom mangler", ownerHtml);
        var print = await publicUser.Client.GetStringAsync("/Resource/RegionalList?region=Agder");
        Assert.Contains(visible.Name, print); Assert.DoesNotContain(hidden.Name, print); Assert.Contains("01.11.2026", print);
    }

    [MariaDbFact]
    public async Task Login_rejects_external_return_url_and_locks_after_five_failures()
    {
        var email = await database.UserAsync("ResourceProvider");
        using var http = new PortalHttp(database.Factory);
        var login = await http.PostAsync("/Account/Login", new() { ["Email"] = email, ["Password"] = PortalHttp.Password,
            ["ReturnUrl"] = "https://example.org/" }, "/Account/Login");
        Assert.Equal("/", login.Headers.Location!.ToString());
        using var wrong = new PortalHttp(database.Factory);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await wrong.PostAsync("/Account/Login", new() { ["Email"] = email,
                ["Password"] = "WrongPassword123" }, "/Account/Login")).StatusCode);
        await using var db = database.Context();
        Assert.True((await db.Users.SingleAsync(user => user.UserName == email)).LockoutEnd > DateTimeOffset.UtcNow);
        var blocked = await wrong.PostAsync("/Account/Login", new() { ["Email"] = email, ["Password"] = PortalHttp.Password }, "/Account/Login");
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        Assert.Contains("midlertidig", await blocked.Content.ReadAsStringAsync());
    }

    [MariaDbFact]
    public async Task Need_HTTP_flow_matches_assigns_releases_and_resolves()
    {
        var email = await database.UserAsync("ResourceProvider"); var resource = await AddResource(await UserId(email));
        var (publicEmail, http) = await Login("PublicUser"); using var session = http;
        var fields = new Dictionary<string, string> { ["Type"] = "Pumpe", ["Region"] = "Agder", ["Location"] = "Kristiansand",
            ["NeededAt"] = "2026-11-06T12:00", ["Priority"] = "High", ["ContactName"] = "Testperson", ["PhoneNumber"] = "+4799999999" };
        Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync("/Need/Register", fields, "/Need/Register")).StatusCode);
        await using var db = database.Context(); var owner = await UserId(publicEmail);
        var need = await db.Needs.AsNoTracking().SingleAsync(row => row.CreatedByUserId == owner);
        Assert.Contains(resource.Name, await http.Client.GetStringAsync($"/Need/Matches/{need.Id}"));
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync("/Need/Assign", new() { ["id"] = need.Id.ToString(),
                ["resourceId"] = resource.Id.ToString() }, "/Need/Register")).StatusCode);
            Assert.False((await db.Resources.AsNoTracking().SingleAsync(row => row.Id == resource.Id)).IsAvailable);
            var path = i == 0 ? "/Need/Release" : "/Need/UpdateStatus";
            Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync(path, new() { ["id"] = need.Id.ToString(),
                ["status"] = "Resolved" }, "/Need/Register")).StatusCode);
            need = await db.Needs.AsNoTracking().SingleAsync(row => row.Id == need.Id);
            Assert.Equal(i == 0 ? "UnderReview" : "Resolved", need.Status); Assert.Null(need.AssignedResourceId);
            Assert.True((await db.Resources.AsNoTracking().SingleAsync(row => row.Id == resource.Id)).IsAvailable);
        }
    }

    [MariaDbFact]
    public async Task Matching_honors_period_boundaries_and_rejects_forged_out_of_period_assignment()
    {
        var owner = await UserId(await database.UserAsync("PublicUser"));
        var provider = await UserId(await database.UserAsync("ResourceProvider"));
        var type = Guid.NewGuid().ToString("N"); var need = await AddNeed(owner, type);
        var resource = await AddResource(provider, " " + type.ToUpperInvariant() + " ");
        await using var db = database.Context(); var service = new AllocationService(db);
        foreach (var time in new[] { resource.AvailableFrom!.Value, resource.AvailableUntil!.Value, need.NeededAt })
        {
            need.NeededAt = time;
            Assert.Contains(await service.FindMatchesAsync(need), row => row.Id == resource.Id);
        }
        await db.Resources.Where(row => row.Id == resource.Id).ExecuteUpdateAsync(update => update
            .SetProperty(row => row.AvailableUntil, new DateTime(2026, 11, 5)));
        Assert.DoesNotContain(await service.FindMatchesAsync(need), row => row.Id == resource.Id);
        Assert.Equal(AllocationResult.Conflict, await service.AssignAsync(need.Id, resource.Id, owner, false));
        Assert.True((await db.Resources.AsNoTracking().SingleAsync(row => row.Id == resource.Id)).IsAvailable);
    }

    [MariaDbFact]
    public async Task Concurrent_needs_cannot_claim_same_resource_and_release_preserves_invariants()
    {
        var owner = await UserId(await database.UserAsync("PublicUser"));
        var provider = await UserId(await database.UserAsync("ResourceProvider"));
        for (var i = 0; i < 3; i++)
        {
            var resource = await AddResource(provider); var first = await AddNeed(owner); var second = await AddNeed(owner);
            await using var one = database.Context(); await using var two = database.Context();
            var results = await Task.WhenAll(new AllocationService(one).AssignAsync(first.Id, resource.Id, owner, false),
                new AllocationService(two).AssignAsync(second.Id, resource.Id, owner, false));
            Assert.Single(results, result => result == AllocationResult.Success);
            Assert.Single(results, result => result == AllocationResult.Conflict);
            await using var check = database.Context();
            var assigned = await check.Needs.AsNoTracking().SingleAsync(row => row.AssignedResourceId == resource.Id);
            Assert.False((await check.Resources.AsNoTracking().SingleAsync(row => row.Id == resource.Id)).IsAvailable);
            Assert.Equal(AllocationResult.Forbidden, await new AllocationService(check).ReleaseAsync(assigned.Id, "stranger", false, false));
            Assert.Equal(AllocationResult.Success, await new AllocationService(check).ReleaseAsync(assigned.Id, owner, false, false));
            Assert.True((await check.Resources.AsNoTracking().SingleAsync(row => row.Id == resource.Id)).IsAvailable);
        }
    }

    [MariaDbFact]
    public async Task Same_need_claim_rolls_back_losing_resource_and_blocks_assigned_edit_delete()
    {
        var owner = await UserId(await database.UserAsync("PublicUser"));
        var (email, http) = await Login(); using var providerHttp = http;
        var provider = await UserId(email); var need = await AddNeed(owner);
        var first = await AddResource(provider); var second = await AddResource(provider);
        await using var one = database.Context(); await using var two = database.Context();
        var results = await Task.WhenAll(new AllocationService(one).AssignAsync(need.Id, first.Id, owner, false),
            new AllocationService(two).AssignAsync(need.Id, second.Id, owner, false));
        Assert.Single(results, result => result == AllocationResult.Success);
        Assert.Single(results, result => result == AllocationResult.Conflict);
        await using var db = database.Context(); need = await db.Needs.AsNoTracking().SingleAsync(row => row.Id == need.Id);
        Assert.Single(await db.Resources.Where(row => row.Id == first.Id || row.Id == second.Id).ToListAsync(), row => row.IsAvailable);
        var fields = PortalHttp.ResourceFields("Forsøk på endring"); fields["Id"] = need.AssignedResourceId.ToString()!;
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsync($"/Resource/Edit/{need.AssignedResourceId}", fields, "/Resource/Register")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsync($"/Resource/Delete/{need.AssignedResourceId}", new(), "/Resource/Register")).StatusCode);
        Assert.True(await db.Resources.AnyAsync(row => row.Id == need.AssignedResourceId));
        Assert.False((await db.Resources.AsNoTracking().SingleAsync(row => row.Id == need.AssignedResourceId)).IsAvailable);
    }

    [MariaDbFact]
    public async Task Data_survives_new_application_host_and_all_migrations_are_applied()
    {
        var provider = await UserId(await database.UserAsync("ResourceProvider")); var resource = await AddResource(provider);
        await using var secondHost = new PortalFactory(database.ConnectionString);
        using var http = new PortalHttp(secondHost);
        (await http.Client.GetAsync("/")).EnsureSuccessStatusCode();
        await using var db = database.Context();
        Assert.Equal(resource.AvailableUntil, (await db.Resources.AsNoTracking().SingleAsync(row => row.Id == resource.Id)).AvailableUntil);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [MariaDbFact]
    public async Task Upgrade_from_previous_release_preserves_legacy_resource_without_inventing_dates()
    {
        var connection = new MySqlConnectionStringBuilder(database.ConnectionString);
        var name = "is202_test_upgrade_" + Guid.NewGuid().ToString("N");
        connection.Database = "";
        await using var sql = new MySqlConnection(connection.ConnectionString); await sql.OpenAsync();
        await new MySqlCommand($"CREATE DATABASE `{name}`", sql).ExecuteNonQueryAsync();
        try
        {
            connection.Database = name;
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(connection.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 0))).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261104234418_AddResourceMatchingFields");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO AspNetUsers (Id,UserName,EmailConfirmed,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount) VALUES ('legacy','legacy',0,0,0,0,0)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO Resources (Name,Type,Region,IsAvailable,ContactName,PhoneNumber,Latitude,Longitude,OwnerId,CreatedAtUtc) VALUES ('Eksisterende pumpe','Pumpe','Agder',1,'Testperson','99999999',58,8,'legacy','2026-11-01')");
            await db.Database.MigrateAsync();
            var resource = await db.Resources.SingleAsync();
            Assert.Equal("Eksisterende pumpe", resource.Name); Assert.True(resource.IsAvailable);
            Assert.Null(resource.AvailableFrom); Assert.Null(resource.AvailableUntil);
            Assert.Empty(await new AllocationService(db).FindMatchesAsync(new Need { Type = "Pumpe", Region = "Agder", NeededAt = new DateTime(2026, 11, 6) }));
        }
        finally
        {
            MySqlConnection.ClearAllPools();
            await new MySqlCommand($"DROP DATABASE `{name}`", sql).ExecuteNonQueryAsync();
        }
    }
}
