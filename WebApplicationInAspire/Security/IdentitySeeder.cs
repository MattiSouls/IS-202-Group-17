using Microsoft.AspNetCore.Identity;

namespace WebApplicationInAspire.Security;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (!environment.IsDevelopment())
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        await CreateTestUserAsync(userManager, configuration, "SeedUsers:PublicUser", RoleNames.PublicUser);
        await CreateTestUserAsync(userManager, configuration, "SeedUsers:Admin", RoleNames.Admin);
    }

    private static async Task CreateTestUserAsync(
        UserManager<IdentityUser> userManager, IConfiguration configuration, string section, string role)
    {
        var email = configuration[$"{section}:Email"];
        var password = configuration[$"{section}:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return; // ingen testkonto konfigurert
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Kunne ikke opprette testkonto {email}: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}