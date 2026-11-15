using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.Models.Entities;

namespace WebApplicationInAspire.Security;

/// <summary>Valgfrie, syntetiske EXPO-data. Kjøres bare i Development og endrer aldri eksisterende rader.</summary>
public static class ExpoDemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DemoData:Enabled")) return;

        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        var provider = await users.FindByEmailAsync(configuration["SeedUsers:Provider:Email"] ?? "");
        var publicUser = await users.FindByEmailAsync(configuration["SeedUsers:PublicUser:Email"] ?? "");
        if (provider is null || publicUser is null
            || !await users.IsInRoleAsync(provider, RoleNames.ResourceProvider)
            || !await users.IsInRoleAsync(publicUser, RoleNames.PublicUser))
            throw new InvalidOperationException("EXPO-data krever konfigurert leverandørkonto og offentlig testkonto.");

        var dateText = configuration["DemoData:Date"];
        var date = string.IsNullOrWhiteSpace(dateText)
            ? TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Oslo").Date.AddDays(1)
            : DateTime.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var db = services.GetRequiredService<ApplicationDbContext>();
        // Faste nøkler hindrer duplikater ved omstart; eksisterende reservasjoner og endringer beholdes.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var offers = new[]
        {
            ("Vannpumpe med slangesett", "Pumpe", "Agder", 58.159, 8.018, true),
            ("Mobil lensepumpe", "Pumpe", "Agder", 58.147, 7.982, true),
            ("Aggregat 20 kW", "Aggregat", "Agder", 58.126, 7.957, true),
            ("Transportbil med sjåfør", "Transport", "Agder", 58.173, 8.053, true),
            ("Pumpe – vedlikehold", "Pumpe", "Agder", 58.152, 8.001, false),
            ("Vannpumpe i Stavanger", "Pumpe", "Rogaland", 58.970, 5.733, true)
        };
        for (var i = 0; i < offers.Length; i++)
        {
            var token = Token(i + 1);
            if (await db.Resources.AnyAsync(r => r.OwnerId == provider.Id && r.CreationToken == token)) continue;
            var (name, type, region, lat, lng, available) = offers[i];
            db.Resources.Add(new Resource
            {
                CreationToken = token, Name = $"EXPO · {name}", Type = type, Region = region,
                Latitude = lat, Longitude = lng, IsAvailable = available,
                AvailableFrom = date.AddHours(8), AvailableUntil = date.AddDays(7).AddHours(18),
                Description = "Syntetisk demonstrasjonsressurs, ikke et reelt tilbud. Kapasitet, transport og praktisk bruk avtales direkte.",
                ContactName = "EXPO demoleverandør", PhoneNumber = "+4700000000", OwnerId = provider.Id
            });
        }

        var needs = new[] { ("Pumpe", "Lund – oversvømt kjeller", "Urgent", 58.159, 8.018), ("Aggregat", "Vågsbygd – strømbrudd", "High", 58.126, 7.957), ("Transport", "Søm – transport av utstyr", "Medium", 58.173, 8.053) };
        for (var i = 0; i < needs.Length; i++)
        {
            var token = Token(100 + i);
            if (await db.Needs.AnyAsync(n => n.CreatedByUserId == publicUser.Id && n.CreationToken == token)) continue;
            var (type, location, priority, lat, lng) = needs[i];
            db.Needs.Add(new Need
            {
                CreationToken = token, Type = type, Region = "Agder", Location = $"EXPO · {location}",
                NeededAt = date.AddHours(13), Priority = priority, Status = NeedStatuses.New,
                Latitude = lat, Longitude = lng,
                ContactName = "EXPO demokommune", PhoneNumber = "+4700000000", CreatedByUserId = publicUser.Id
            });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static Guid Token(int number) => Guid.Parse($"e1602026-0000-4000-8000-{number:000000000000}");
}
