using LicenseSystem.Core.Cryptography;
using LicenseSystem.Core.Interfaces;
using LicenseSystem.Infrastructure.Data;
using LicenseSystem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure PostgreSQL DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=license_management;Username=license_user;Password=license_secure_pass_2026";

builder.Services.AddDbContext<LicenseDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register Repositories
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ILicenseRepository, LicenseRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddSingleton<ISignatureVerifier, EcdsaP256SignatureVerifier>();

// Register Server-Side Signing Service
builder.Services.AddSingleton<ISigningService>(sp =>
{
    var configKey = builder.Configuration["Licensing:PrivateKeyPem"];
    var keysDir = Path.Combine(AppContext.BaseDirectory, "keys");
    Directory.CreateDirectory(keysDir);
    var privateKeyFile = Path.Combine(keysDir, "server_private_key.pem");
    var publicKeyFile = Path.Combine(keysDir, "server_public_key.pem");

    if (!string.IsNullOrWhiteSpace(configKey))
    {
        return new EcdsaP256SigningService(configKey);
    }

    if (File.Exists(privateKeyFile))
    {
        var pem = File.ReadAllText(privateKeyFile);
        return new EcdsaP256SigningService(pem);
    }

    // Generate new persistent key pair
    var newService = new EcdsaP256SigningService();
    var privateKeyPem = $"-----BEGIN PRIVATE KEY-----\n{newService.ExportPrivateKeyPkcs8()}\n-----END PRIVATE KEY-----";
    var publicKeyPem = $"-----BEGIN PUBLIC KEY-----\n{newService.GetPublicKeySpki()}\n-----END PUBLIC KEY-----";

    File.WriteAllText(privateKeyFile, privateKeyPem);
    File.WriteAllText(publicKeyFile, publicKeyPem);

    return newService;
});

var app = builder.Build();

// Automatically ensure database schema exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();

app.Run();

// Partial class for WebApplicationFactory in tests
public partial class Program { }
