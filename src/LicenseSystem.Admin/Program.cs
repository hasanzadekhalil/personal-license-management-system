using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Infrastructure.Data;
using LicenseSystem.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "LicenseAdminAuth";
    });

builder.Services.AddAuthorization();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Logout");
});

// Configure PostgreSQL DbContext with SplitQuery
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=license_management;Username=license_user;Password=license_secure_pass_2026";

builder.Services.AddDbContext<LicenseDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

// Register Repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ILicenseRepository, LicenseRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddSingleton<ISignatureVerifier, EcdsaP256SignatureVerifier>();

// Register Signing Service
builder.Services.AddSingleton<ISigningService>(sp =>
{
    var keysDir = Path.Combine(AppContext.BaseDirectory, "keys");
    Directory.CreateDirectory(keysDir);
    var privateKeyFile = Path.Combine(keysDir, "server_private_key.pem");
    var publicKeyFile = Path.Combine(keysDir, "server_public_key.pem");

    if (File.Exists(privateKeyFile))
    {
        var pem = File.ReadAllText(privateKeyFile);
        return new EcdsaP256SigningService(pem);
    }

    var newService = new EcdsaP256SigningService();
    var privateKeyPem = $"-----BEGIN PRIVATE KEY-----\n{newService.ExportPrivateKeyPkcs8()}\n-----END PRIVATE KEY-----";
    var publicKeyPem = $"-----BEGIN PUBLIC KEY-----\n{newService.GetPublicKeySpki()}\n-----END PUBLIC KEY-----";

    File.WriteAllText(privateKeyFile, privateKeyPem);
    File.WriteAllText(publicKeyFile, publicKeyPem);

    return newService;
});

var app = builder.Build();

// Automatically ensure database schema exists and seed default desktop products
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    db.Database.EnsureCreated();
    DatabaseSeeder.SeedDefaultDataAsync(db).GetAwaiter().GetResult();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
