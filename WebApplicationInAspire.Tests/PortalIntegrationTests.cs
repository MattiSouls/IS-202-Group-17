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
            Name = "Pumpe " + Guid.NewGuid().ToString("N"),
            Type = type ?? "Pumpe",
            Region = "Agder",
            OwnerId = owner,
            ContactName = "Testperson",
            PhoneNumber = "+4799999999",
            Latitude = 58.1467,
            Longitude = 7.9956,
            AvailableFrom = new DateTime(2026, 11, 1),
            AvailableUntil = new DateTime(2026, 11, 30)
        };
        db.Resources.Add(resource); await db.SaveChangesAsync(); return resource;
    }
    private async Task<Need> AddNeed(string owner, string type = "Pumpe")
    {
        await using var db = database.Context();
        var need = new Need
        {
            Type = type,
            Region = "Agder",
            NeededAt = new DateTime(2026, 11, 6, 12, 0, 0),
            CreatedByUserId = owner,
            Priority = "High",
            ContactName = "Testperson",
            PhoneNumber = "+4799999999"
        };
        db.Needs.Add(need); await db.SaveChangesAsync(); return need;
    }
    private static void Denied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [MariaDbFact]
    public async Task Landing_exposes_need_registration_and_login_returns_public_user_to_the_form()
    {
        using var http = new PortalHttp(database.Factory);
        var landing = await http.Client.GetStringAsync("/");
        Assert.Contains("href=\"/Need/Register\"", landing);
        Assert.Contains("Registrer behov", landing);
        Assert.Equal(HttpStatusCode.Redirect, (await http.Client.GetAsync("/Need/Register")).StatusCode);
        var email = await database.UserAsync("PublicUser");
        var response = await http.PostAsync("/Account/Login", new()
        {
            ["Email"] = email, ["Password"] = PortalHttp.Password, ["ReturnUrl"] = "/Need/Register"
        }, "/Account/Login?returnUrl=%2FNeed%2FRegister");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Need/Register", response.Headers.Location!.ToString());
        Assert.Contains("name=\"NeededAt\"", await http.Client.GetStringAsync("/Need/Register"));
    }

    [MariaDbFact]
    public async Task Registration_stores_hashed_password_and_cannot_choose_admin_role()
    {
        using var http = new PortalHttp(database.Factory);
        var email = $"{Guid.NewGuid():N}@example.test";
        var response = await http.PostAsync("/Account/Register", new()
        {
            ["Email"] = email,
            ["Password"] = PortalHttp.Password,
            ["ConfirmPassword"] = PortalHttp.Password,
            ["Role"] = "Admin"
        }, "/Account/Register");
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
        var prefix = "Scoped-" + Guid.NewGuid().ToString("N");
        visible.Name = prefix + " visible"; hidden.Name = prefix + " hidden"; legacy.Name = prefix + " legacy";
        await using (var db = database.Context())
        {
            await db.Resources.Where(r => r.Id == visible.Id).ExecuteUpdateAsync(u => u.SetProperty(r => r.Name, visible.Name).SetProperty(r => r.Region, prefix));
            await db.Resources.Where(r => r.Id == hidden.Id).ExecuteUpdateAsync(u => u.SetProperty(r => r.Name, hidden.Name).SetProperty(r => r.Region, prefix));
            await db.Resources.Where(r => r.Id == legacy.Id).ExecuteUpdateAsync(u => u.SetProperty(r => r.Name, legacy.Name).SetProperty(r => r.Region, prefix));
            await db.Resources.Where(row => row.Id == hidden.Id).ExecuteUpdateAsync(update => update.SetProperty(row => row.IsAvailable, false));
            await db.Resources.Where(row => row.Id == legacy.Id).ExecuteUpdateAsync(update => update.SetProperty(row => row.AvailableFrom, (DateTime?)null));
        }
        var publicHtml = await publicUser.Client.GetStringAsync("/Resource/Overview?region=" + prefix);
        Assert.Contains(visible.Name, publicHtml); Assert.DoesNotContain(hidden.Name, publicHtml); Assert.DoesNotContain(legacy.Name, publicHtml);
        var ownerHtml = await owner.Client.GetStringAsync("/Resource/Overview");
        Assert.Contains(hidden.Name, ownerHtml); Assert.Contains("Tidsrom mangler", ownerHtml);
        var print = await publicUser.Client.GetStringAsync("/Resource/RegionalList?region=" + prefix);
        Assert.Contains(visible.Name, print); Assert.DoesNotContain(hidden.Name, print); Assert.Contains("01.11.2026", print);
    }

    [MariaDbFact]
    public async Task Login_rejects_external_return_url_and_locks_after_five_failures()
    {
        var email = await database.UserAsync("ResourceProvider");
        using var http = new PortalHttp(database.Factory);
        var login = await http.PostAsync("/Account/Login", new()
        {
            ["Email"] = email,
            ["Password"] = PortalHttp.Password,
            ["ReturnUrl"] = "https://example.org/"
        }, "/Account/Login");
        Assert.Equal("/", login.Headers.Location!.ToString());
        using var wrong = new PortalHttp(database.Factory);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await wrong.PostAsync("/Account/Login", new()
            {
                ["Email"] = email,
                ["Password"] = "WrongPassword123"
            }, "/Account/Login")).StatusCode);
        await using var db = database.Context();
        Assert.True((await db.Users.SingleAsync(user => user.UserName == email)).LockoutEnd > DateTimeOffset.UtcNow);
        var blocked = await wrong.PostAsync("/Account/Login", new() { ["Email"] = email, ["Password"] = PortalHttp.Password }, "/Account/Login");
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        Assert.Contains("midlertidig", await blocked.Content.ReadAsStringAsync());
    }

    [MariaDbFact]
    public async Task Need_HTTP_flow_matches_assigns_releases_and_resolves()
    {
        var uniqueType = "Pumpe-" + Guid.NewGuid().ToString("N");
        var email = await database.UserAsync("ResourceProvider"); var resource = await AddResource(await UserId(email), uniqueType);
        var (publicEmail, http) = await Login("PublicUser"); using var session = http;
        var fields = new Dictionary<string, string>
        {
            ["Type"] = uniqueType,
            ["Region"] = "Agder",
            ["Location"] = "Kristiansand",
            ["NeededAt"] = "2026-11-06T12:00",
            ["Priority"] = "High",
            ["ContactName"] = "Testperson",
            ["PhoneNumber"] = "+4799999999"
        };
        Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync("/Need/Register", fields, "/Need/Register")).StatusCode);
        await using var db = database.Context(); var owner = await UserId(publicEmail);
        var need = await db.Needs.AsNoTracking().SingleAsync(row => row.CreatedByUserId == owner);
        Assert.Contains(resource.Name, await http.Client.GetStringAsync($"/Need/Matches/{need.Id}"));
        for (var i = 0; i < 2; i++)
        {
            need = await db.Needs.AsNoTracking().SingleAsync(n => n.Id == need.Id);
            resource = await db.Resources.AsNoTracking().SingleAsync(r => r.Id == resource.Id);
            Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync("/Need/Assign", new()
            {
                ["needVersion"] = need.Version.ToString(),
                ["resourceVersion"] = resource.Version.ToString(),
                ["id"] = need.Id.ToString(),
                ["resourceId"] = resource.Id.ToString()
            }, "/Need/Register")).StatusCode);
            Assert.False((await db.Resources.AsNoTracking().SingleAsync(row => row.Id == resource.Id)).IsAvailable);
            var path = i == 0 ? "/Need/Release" : "/Need/UpdateStatus";
            Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync(path, new()
            {
                ["id"] = need.Id.ToString(),
                ["needVersion"] = (need.Version + 1).ToString(),
                ["status"] = "Resolved"
            }, "/Need/Register")).StatusCode);
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

    [MariaDbFact]
    public async Task Replayed_resource_and_need_registration_are_idempotent_but_new_form_can_repeat_content()
    {
        var (email, http) = await Login(); using var session = http;
        var fields = PortalHttp.ResourceFields("Idempotent " + Guid.NewGuid().ToString("N"));
        fields["RequestId"] = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(http.PostAsync("/Resource/Register", fields, "/Resource/Register"), http.PostAsync("/Resource/Register", fields, "/Resource/Register"));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));
        await using var db = database.Context();
        Assert.Equal(1, await db.Resources.CountAsync(r => r.Name == fields["Name"]));
        fields["RequestId"] = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync("/Resource/Register", fields, "/Resource/Register")).StatusCode);
        Assert.Equal(2, await db.Resources.CountAsync(r => r.Name == fields["Name"]));
        var (publicEmail, publicHttp) = await Login("PublicUser"); using var actor = publicHttp;
        var needFields = new Dictionary<string, string> { ["Type"] = "Pumpe", ["Region"] = "Agder", ["NeededAt"] = "2026-11-06T12:00", ["Priority"] = "High", ["ContactName"] = "Testperson", ["PhoneNumber"] = "99999999", ["RequestId"] = Guid.NewGuid().ToString() };
        var created = await Task.WhenAll(actor.PostAsync("/Need/Register", needFields, "/Need/Register"), actor.PostAsync("/Need/Register", needFields, "/Need/Register"));
        Assert.All(created, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));
        var id = await UserId(publicEmail); Assert.Equal(1, await db.Needs.CountAsync(n => n.CreatedByUserId == id));
    }

    [MariaDbFact]
    public async Task Stale_edit_is_rejected_without_losing_newer_data_or_entered_values()
    {
        var (email, http) = await Login(); using var actor = http;
        var resource = await AddResource(await UserId(email));
        var fields = PortalHttp.ResourceFields(resource.Name); fields["Id"] = resource.Id.ToString(); fields["Version"] = "0";
        fields["PhoneNumber"] = "98888888";
        Assert.Equal(HttpStatusCode.Redirect, (await http.PostAsync($"/Resource/Edit/{resource.Id}", fields, "/Resource/Register")).StatusCode);
        fields["Name"] = "Dette skrev jeg i en gammel fane"; fields["PhoneNumber"] = "97777777";
        var stale = await http.PostAsync($"/Resource/Edit/{resource.Id}", fields, "/Resource/Register");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Contains(fields["Name"], await stale.Content.ReadAsStringAsync());
        await using var db = database.Context(); var stored = await db.Resources.AsNoTracking().SingleAsync(r => r.Id == resource.Id);
        Assert.Equal("98888888", stored.PhoneNumber); Assert.Equal(resource.Name, stored.Name); Assert.Equal(1, stored.Version);
    }

    [MariaDbFact]
    public async Task Assignment_notices_commit_once_and_are_private_even_when_read_by_forged_id()
    {
        var (providerEmail, providerHttp) = await Login(); using var provider = providerHttp;
        var owner = await UserId(await database.UserAsync("PublicUser"));
        var resource = await AddResource(await UserId(providerEmail)); var first = await AddNeed(owner); var second = await AddNeed(owner);
        await using var one = database.Context(); await using var two = database.Context();
        var results = await Task.WhenAll(new AllocationService(one).AssignAsync(first.Id, resource.Id, owner, false), new AllocationService(two).AssignAsync(second.Id, resource.Id, owner, false));
        Assert.Single(results, r => r == AllocationResult.Success);
        await using var db = database.Context(); var notices = await db.Notifications.AsNoTracking().Where(n => n.ResourceId == resource.Id).ToListAsync();
        Assert.Equal(2, notices.Count); var winner = await db.Needs.AsNoTracking().SingleAsync(n => n.AssignedResourceId == resource.Id);
        Assert.All(notices, n => Assert.Equal(winner.Id, n.NeedId));
        Assert.Equal(AllocationResult.Conflict, await new AllocationService(db).AssignAsync(winner.Id, resource.Id, owner, false));
        Assert.Equal(2, await db.Notifications.CountAsync(n => n.ResourceId == resource.Id));
        var detail = await provider.Client.GetStringAsync($"/Resource/Details/{resource.Id}"); Assert.Contains(winner.ContactName, detail); Assert.Contains(winner.PhoneNumber, System.Net.WebUtility.HtmlDecode(detail));
        var html = await provider.Client.GetStringAsync("/Notifications"); Assert.Contains("Ressurs tildelt", html);
        var ownNotice = notices.Single(n => n.UserId == resource.OwnerId); var otherNotice = notices.Single(n => n.UserId == owner);
        Assert.Equal(HttpStatusCode.NotFound, (await provider.PostAsync("/Notifications/Read", new() { ["id"] = otherNotice.Id.ToString() }, "/Resource/Register")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await provider.PostAsync("/Notifications/Read", new() { ["id"] = ownNotice.Id.ToString() }, "/Resource/Register")).StatusCode);
        Assert.NotNull((await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == ownNotice.Id)).ReadAtUtc);
        Assert.Null((await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == otherNotice.Id)).ReadAtUtc);
        Assert.Equal(AllocationResult.Success, await new AllocationService(db).ReleaseAsync(winner.Id, owner, false, false));
        Assert.Equal(4, await db.Notifications.CountAsync(n => n.ResourceId == resource.Id));
    }

    [MariaDbFact]
    public async Task Matching_sorts_real_coordinates_in_SQL_and_review_does_not_leak_non_candidates()
    {
        var (email, http) = await Login("PublicUser"); using var actor = http; var owner = await UserId(email);
        var provider = await UserId(await database.UserAsync("ResourceProvider")); var type = Guid.NewGuid().ToString("N");
        var need = await AddNeed(owner, type); var far = await AddResource(provider, type); var near = await AddResource(provider, type);
        await using var db = database.Context();
        await db.Needs.Where(n => n.Id == need.Id).ExecuteUpdateAsync(u => u.SetProperty(n => n.Latitude, 58.1467).SetProperty(n => n.Longitude, 7.9956));
        await db.Resources.Where(r => r.Id == far.Id).ExecuteUpdateAsync(u => u.SetProperty(r => r.Latitude, 58.5).SetProperty(r => r.Longitude, 8.5));
        need = await db.Needs.AsNoTracking().SingleAsync(n => n.Id == need.Id);
        var matches = await new AllocationService(db).FindMatchesAsync(need); Assert.Equal(near.Id, matches[0].Id);
        var html = await actor.Client.GetStringAsync($"/Need/Matches/{need.Id}"); Assert.Contains("luftlinje", html);
        var privateResource = await AddResource(provider, "not-a-match");
        var response = await actor.Client.GetAsync($"/Need/Review/{need.Id}?resourceId={privateResource.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.DoesNotContain(privateResource.Name, await response.Content.ReadAsStringAsync());
        var review = await actor.Client.GetStringAsync($"/Need/Review/{need.Id}?resourceId={near.Id}"); Assert.Contains("Bekreft tildeling", review);
        Assert.False(await db.Needs.AnyAsync(n => n.Id == need.Id && n.Status == "Assigned"));
    }

    [MariaDbFact]
    public async Task Open_need_can_be_corrected_but_other_owner_and_stale_form_cannot_overwrite_it()
    {
        var (email, http) = await Login("PublicUser"); using var actor = http; var owner = await UserId(email); var need = await AddNeed(owner);
        var fields = new Dictionary<string, string> { ["Id"] = need.Id.ToString(), ["Version"] = "0", ["Type"] = "Aggregat", ["Region"] = "Agder", ["NeededAt"] = "2026-11-06T12:00", ["Priority"] = "High", ["ContactName"] = "Testperson", ["PhoneNumber"] = "99999999" };
        Assert.Equal(HttpStatusCode.Redirect, (await actor.PostAsync($"/Need/Edit/{need.Id}", fields, "/Need/Register")).StatusCode);
        fields["Type"] = "Gammelt skjema"; Assert.Equal(HttpStatusCode.Conflict, (await actor.PostAsync($"/Need/Edit/{need.Id}", fields, "/Need/Register")).StatusCode);
        var (_, stranger) = await Login("PublicUser"); using var other = stranger; Denied(await other.PostAsync($"/Need/Edit/{need.Id}", fields, "/Need/Register"));
        await using var db = database.Context(); Assert.Equal("Aggregat", (await db.Needs.AsNoTracking().SingleAsync(n => n.Id == need.Id)).Type);
    }

    [MariaDbFact]
    public async Task Ten_thousand_resources_and_five_thousand_needs_render_only_one_bounded_page()
    {
        var (email, http) = await Login(); using var provider = http; var owner = await UserId(email);
        var publicEmail = await database.UserAsync("PublicUser"); var publicId = await UserId(publicEmail);
        await using var db = database.Context();
        for (var batch = 0; batch < 10; batch++)
        {
            db.Resources.AddRange(Enumerable.Range(batch * 1000, 1000).Select(i => new Resource { Name = $"Lasttest {i:D5}", Type = "Pumpe", Region = "Agder", OwnerId = owner, ContactName = "Test", PhoneNumber = "99999999", AvailableFrom = new DateTime(2026, 11, 1), AvailableUntil = new DateTime(2026, 11, 30), Latitude = 58, Longitude = 8 }));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        for (var batch = 0; batch < 5; batch++)
        {
            db.Needs.AddRange(Enumerable.Range(batch * 1000, 1000).Select(i => new Need { Type = $"Testbehov {i:D5}", Region = "Agder", CreatedByUserId = publicId, ContactName = "Test", PhoneNumber = "99999999", Priority = "Low", NeededAt = new DateTime(2026, 11, 6) }));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        var page = await new WebApplicationInAspire.DataAccess.Repositories.ResourceRepository(db).SearchAsync(owner, false, new WebApplicationInAspire.Models.ListFilter { Page = 2 });
        Assert.Equal(10000, page.Total); Assert.Equal(10, page.Resources.Count); Assert.Equal("Lasttest 00010", page.Resources[0].Name);
        var needs = await new WebApplicationInAspire.DataAccess.Repositories.NeedRepository(db).SearchAsync(new WebApplicationInAspire.Models.ListFilter { Mine = true, Page = 2 }, publicId);
        Assert.Equal(5000, needs.Total); Assert.Equal(10, needs.Needs.Count);
        var html = await provider.Client.GetStringAsync("/Resource/Overview?region=Agder&q=Lasttest&page=2");
        Assert.Contains("Lasttest 00010", html); Assert.DoesNotContain("Lasttest 00020", html); Assert.Contains("q=Lasttest", html);
        Assert.True(html.Length < 60000, $"HTML should be bounded, got {html.Length} characters");
    }

    [MariaDbFact]
    public async Task Old_confirmation_cannot_release_a_new_assignment_or_notify_anyone()
    {
        var owner = await UserId(await database.UserAsync("PublicUser")); var provider = await UserId(await database.UserAsync("ResourceProvider"));
        var need = await AddNeed(owner); var first = await AddResource(provider); var second = await AddResource(provider);
        await using var db = database.Context(); var service = new AllocationService(db);
        Assert.Equal(AllocationResult.Success, await service.AssignAsync(need.Id, first.Id, owner, false, 0, 0));
        Assert.Equal(AllocationResult.Success, await service.ReleaseAsync(need.Id, owner, false, false, 1));
        Assert.False(await new WebApplicationInAspire.DataAccess.Repositories.ResourceRepository(db).UpdateAsync(first));
        Assert.Equal(AllocationResult.Success, await service.AssignAsync(need.Id, second.Id, owner, false, 2, 0));
        var count = await db.Notifications.CountAsync(n => n.NeedId == need.Id);
        Assert.Equal(AllocationResult.Conflict, await service.ReleaseAsync(need.Id, owner, false, true, 1));
        var stored = await db.Needs.AsNoTracking().SingleAsync(n => n.Id == need.Id); Assert.Equal(second.Id, stored.AssignedResourceId); Assert.Equal("Assigned", stored.Status);
        Assert.False((await db.Resources.AsNoTracking().SingleAsync(r => r.Id == second.Id)).IsAvailable);
        Assert.Equal(count, await db.Notifications.CountAsync(n => n.NeedId == need.Id));
    }

    [MariaDbFact]
    public async Task Concurrent_need_edit_invalidates_in_flight_assignment_and_rolls_everything_back()
    {
        var owner = await UserId(await database.UserAsync("PublicUser")); var provider = await UserId(await database.UserAsync("ResourceProvider"));
        var need = await AddNeed(owner); var resource = await AddResource(provider);
        var pause = new PauseBeforeResourceRead();
        await using var assigning = new WebApplicationInAspire.DataAccess.ApplicationDbContext(new DbContextOptionsBuilder<WebApplicationInAspire.DataAccess.ApplicationDbContext>()
            .UseMySql(database.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 0))).AddInterceptors(pause).Options);
        var task = new AllocationService(assigning).AssignAsync(need.Id, resource.Id, owner, false);
        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var editing = database.Context(); need.Type = "Aggregat";
            Assert.True(await new WebApplicationInAspire.DataAccess.Repositories.NeedRepository(editing).EditAsync(need));
        }
        finally { pause.Continue.TrySetResult(); }
        Assert.Equal(AllocationResult.Conflict, await task);
        await using var check = database.Context(); Assert.Null((await check.Needs.AsNoTracking().SingleAsync(n => n.Id == need.Id)).AssignedResourceId);
        Assert.True((await check.Resources.AsNoTracking().SingleAsync(r => r.Id == resource.Id)).IsAvailable);
        Assert.Equal(0, await check.Notifications.CountAsync(n => n.NeedId == need.Id));
        Assert.Equal(0, await check.AllocationEvents.CountAsync(n => n.NeedId == need.Id));
    }

    [MariaDbFact]
    public async Task Regional_export_streams_all_rows_scopes_owners_and_neutralizes_spreadsheet_formulas()
    {
        var (email, http) = await Login(); using var actor = http;
        var owner = await UserId(email); var stranger = await UserId(await database.UserAsync("ResourceProvider"));
        var region = "Export-" + Guid.NewGuid().ToString("N");
        await using var db = database.Context();
        db.Resources.AddRange(Enumerable.Range(0, 501).Select(i => new Resource { Name = $"Export {i:D4}", Type = "Pumpe", Region = region, OwnerId = owner, ContactName = "Test", PhoneNumber = "99999999", AvailableFrom = new DateTime(2026, 11, 1), AvailableUntil = new DateTime(2026, 11, 30), Latitude = 58, Longitude = 8 }));
        db.Resources.Add(new Resource { Name = "=HYPERLINK(\"https://example.test\")", Type = "Pumpe", Region = region, OwnerId = owner, ContactName = "+formula", PhoneNumber = "99999999" });
        db.Resources.Add(new Resource { Name = "OTHER_OWNER_SECRET", Type = "Pumpe", Region = region, OwnerId = stranger, ContactName = "Test", PhoneNumber = "99999999" });
        await db.SaveChangesAsync();
        var csv = await actor.Client.GetStringAsync("/Resource/ExportCsv?region=" + region);
        Assert.Equal(503, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("Export 0500", csv); Assert.DoesNotContain("OTHER_OWNER_SECRET", csv);
        Assert.Contains("\"'=HYPERLINK(\"\"https://example.test\"\")\"", csv); Assert.Contains("\"'+formula\"", csv);
        var print = await actor.Client.GetStringAsync("/Resource/RegionalList?region=" + region);
        Assert.Contains("Listen er avkortet", print); Assert.Contains("500", print);
    }

    [MariaDbFact]
    public async Task Security_headers_local_assets_and_accessible_server_errors_are_present()
    {
        var (email, http) = await Login(); using var actor = http;
        await AddResource(await UserId(email));
        var response = await actor.Client.GetAsync("/Resource/Overview");
        var html = await response.Content.ReadAsStringAsync();
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self' 'nonce-", csp); Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Contains("/lib/leaflet/leaflet.js", html); Assert.DoesNotContain("unpkg.com", html);
        var token = System.Text.RegularExpressions.Regex.Match(csp, "nonce-([^']+)").Groups[1].Value;
        Assert.Contains("nonce=\"" + token + "\"", System.Net.WebUtility.HtmlDecode(html));
        var fields = PortalHttp.ResourceFields("Feiltest"); fields["Latitude"] = "999";
        var invalid = await actor.PostAsync("/Resource/Register", fields, "/Resource/Register");
        var errors = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("data-focus-errors", errors); Assert.Contains("href=\"#Latitude\"", errors);
        Assert.Contains("id=\"Latitude-error\"", errors); Assert.Contains("aria-invalid=\"true\"", errors);
    }
}


/// <summary>Deterministisk kappløpstest: behovet er lest før redigeringen slippes til.</summary>
public sealed class PauseBeforeResourceRead : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
{
    public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int paused;
    public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("FROM `Resources`") && Interlocked.CompareExchange(ref paused, 1, 0) == 0)
        {
            Reached.TrySetResult(); await Continue.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        return result;
    }
}
