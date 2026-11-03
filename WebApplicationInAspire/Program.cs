using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplicationInAspire.DataAccess;
using WebApplicationInAspire.DataAccess.Repositories;

// Sikrer at desimaltall med punktum (fra Leaflet/kart) parses likt uavhengig av PC-ens språkinnstilling
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("IS-202-Group-17");
var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
if (!string.IsNullOrWhiteSpace(keyDirectory))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection mangler. Start med Docker Compose, " +
        "eller konfigurer lokal databasekobling med User Secrets.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 4, 0))));
builder.Services.AddScoped<IResourceRepository, ResourceRepository>();

// Identity: brukere og roller lagres i MariaDB via ApplicationDbContext.
// Passord lagres som hash av Identity, aldri i klartekst.
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.SignIn.RequireConfirmedAccount = false; // ingen e-posttjeneste i prosjektet
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders(); // trengs for tofaktor med autentikator-app

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
});

var app = builder.Build();

// Compose slår dette på for lokal utvikling. Andre miljøer bruker eksplisitte migrasjoner.
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();

// Authentication må komme før Authorization, ellers kjenner ikke appen igjen innlogget bruker.
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
