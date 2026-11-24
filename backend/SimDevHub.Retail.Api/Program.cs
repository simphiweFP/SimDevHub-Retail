using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("RetailDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:RetailDb using environment variables or a secret manager.");

builder.Services.AddDbContext<RetailDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IStorefrontCatalogue, StorefrontCatalogueReader>();
builder.Services.AddScoped<IStaffAccess, StaffAccessReader>();
builder.Services.AddHealthChecks();

var identityAuthority = builder.Configuration["Identity:Authority"];
var identityAudience = builder.Configuration["Identity:Audience"];
var identityReady = !string.IsNullOrWhiteSpace(identityAuthority) &&
                    !string.IsNullOrWhiteSpace(identityAudience);
if (identityReady && !Uri.TryCreate(identityAuthority, UriKind.Absolute, out var issuerUri))
    throw new InvalidOperationException("Identity:Authority must be a valid HTTPS URL.");
if (identityReady && !identityAuthority!.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Identity:Authority must use HTTPS.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (identityReady)
        {
            options.Authority = identityAuthority;
            options.Audience = identityAudience;
            options.RequireHttpsMetadata = true;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                NameClaimType = "sub"
            };
        }
        else
        {
            // No login system is available without an explicitly configured trusted authority.
            // The staff endpoint below is not registered until configuration is complete.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    ctx.NoResult();
                    return Task.CompletedTask;
                }
            };
        }
    });
builder.Services.AddAuthorization();

var app = builder.Build();
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Retail:SeedDemo"))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<RetailDbContext>();
    await database.Database.MigrateAsync();
    await DevelopmentSeed.SeedAsync(database);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");

app.MapGet("/api/storefront/{storeSlug}/products", async (
    string storeSlug, IStorefrontCatalogue catalogue, CancellationToken cancellationToken) =>
{
    if (storeSlug.Length is < 2 or > 80 ||
        !storeSlug.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
        return Results.BadRequest(new { error = "Invalid store slug." });
    var store = await catalogue.GetPublishedAsync(storeSlug, cancellationToken);
    return store is null ? Results.NotFound() : Results.Ok(store);
}).WithName("GetStorefrontProducts");

if (identityReady)
{
    app.MapGet("/api/staff/{storeSlug}/me", async (
        string storeSlug, ClaimsPrincipal principal, IStaffAccess access, CancellationToken ct) =>
    {
        // Tokens must be validated by the configured OIDC/JWT authority.
        // The external provider subject is not a tenant membership by itself.
        var issuer = principal.FindFirst("iss")?.Value;
        var subject = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
            return Results.Forbid();
        var membership = await access.ResolveAsync(issuer, subject, storeSlug, ct);
        return membership is null ? Results.Forbid() : Results.Ok(membership);
    }).RequireAuthorization();
}

app.Run();
public partial class Program;
