using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
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
builder.Services.AddScoped<IStaffInvitations, StaffInvitationService>();
builder.Services.AddScoped<IInventoryReceiving, InventoryReceivingService>();
builder.Services.AddScoped<IOrderReservationService, OrderReservationService>();
builder.Services.AddScoped<IReservationExpiry, ReservationExpiryService>();
builder.Services.AddScoped<IVerifiedPaymentProcessor, VerifiedPaymentProcessor>();
builder.Services.AddScoped<IGatewayNotificationCoordinator, GatewayNotificationCoordinator>();
builder.Services.AddScoped<IWebhookReceiptProcessor, WebhookReceiptProcessor>();
builder.Services.AddScoped<IPayfastSandboxMerchant, ConfiguredPayfastSandboxMerchant>();
// No Payfast webhook verifier is registered: public payment confirmation remains disabled.
builder.Services.AddScoped<IPaymentIntentService, PaymentIntentService>();
builder.Services.AddScoped<IPaymentReconciliationReader, PaymentReconciliationReader>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("invite-redemption", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
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

app.UseRateLimiter();
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

    app.MapGet("/api/staff/{storeSlug}/payment-exceptions", async (
        string storeSlug, ClaimsPrincipal principal, IStaffAccess access,
        IPaymentReconciliationReader reconciliation, CancellationToken ct) =>
    {
        // Never accept tenant IDs or roles from the request query or headers.
        var issuer = principal.FindFirst("iss")?.Value;
        var subject = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
            return Results.Forbid();
        var membership = await access.ResolveAsync(issuer, subject, storeSlug, ct);
        if (membership is null ||
            membership.Role is not ("Owner" or "Manager" or "FinanceOfficer"))
            return Results.Forbid();
        var exceptions = await reconciliation.GetUnresolvedAsync(membership.TenantId, 50, ct);
        return Results.Ok(exceptions);
    }).RequireAuthorization();

    app.MapPost("/api/staff/invitations/accept", async (
        AcceptInvitationRequest request, ClaimsPrincipal principal,
        IStaffInvitations invitations, CancellationToken ct) =>
    {
        // No client-supplied role, tenant, issuer, subject or email is trusted.
        // Both verified email and JWT identity must come from the validated authority.
        var issuer = principal.FindFirst("iss")?.Value;
        var subject = principal.FindFirst("sub")?.Value;
        var email = principal.FindFirst("email")?.Value;
        var emailVerified = principal.FindFirst("email_verified")?.Value;
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject) ||
            string.IsNullOrWhiteSpace(email) ||
            !string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        var result = await invitations.AcceptAsync(
            request.Token ?? "", new VerifiedStaffIdentity(issuer, subject, email), ct);
        return result == InvitationAcceptStatus.Accepted
            ? Results.Ok(new { message = "Invitation accepted." })
            : Results.BadRequest(new { error = "Invitation unavailable or already used." });
    })
    .RequireAuthorization()
    .RequireRateLimiting("invite-redemption")
    .DisableAntiforgery(); // Bearer-token API; do not add cookie auth without CSRF protection.
}

app.Run();
public sealed record AcceptInvitationRequest(string? Token);
public partial class Program;
