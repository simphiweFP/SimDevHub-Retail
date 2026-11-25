using System.Security.Claims;
using SimDevHub.Retail.Api;
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
builder.Services.AddScoped<ICustomerOrderAccess, CustomerOrderAccess>();
builder.Services.AddScoped<ICustomerOnboarding, CustomerOnboardingService>();
builder.Services.AddScoped<IStaffInvitations, StaffInvitationService>();
builder.Services.AddScoped<IInventoryReceiving, InventoryReceivingService>();
builder.Services.AddScoped<IOrderReservationService, OrderReservationService>();
builder.Services.AddScoped<ICustomerReservationService, CustomerReservationService>();
builder.Services.AddScoped<ICustomerProductReservationService, CustomerProductReservationService>();
builder.Services.AddScoped<IReservationExpiry, ReservationExpiryService>();
builder.Services.AddScoped<IVerifiedPaymentProcessor, VerifiedPaymentProcessor>();
builder.Services.AddScoped<IGatewayNotificationCoordinator, GatewayNotificationCoordinator>();
builder.Services.AddScoped<IWebhookReceiptProcessor, WebhookReceiptProcessor>();
builder.Services.AddScoped<IPayfastSandboxMerchant, ConfiguredPayfastSandboxMerchant>();
builder.Services.AddSingleton<PayfastTrustedSourceValidator>();
builder.Services.AddSingleton<IPayfastSourceValidator>(sp =>
    sp.GetRequiredService<PayfastTrustedSourceValidator>());
builder.Services.AddHttpClient<IPayfastServerConfirmation, PayfastSandboxServerConfirmation>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);
});
// No Payfast webhook verifier is registered: public payment confirmation remains disabled.
builder.Services.AddScoped<IPaymentIntentService, PaymentIntentService>();
builder.Services.AddScoped<IPayfastSandboxCheckoutStart, PayfastSandboxCheckoutStart>();
builder.Services.AddScoped<ICustomerSandboxPayment, CustomerSandboxPayment>();
builder.Services.AddScoped<IPaymentReconciliationReader, PaymentReconciliationReader>();
builder.Services.AddSingleton(TimeProvider.System);
var expiryWorkerEnabled = builder.Configuration.GetValue<bool>("Retail:EnableReservationExpiryWorker");
if (builder.Configuration.GetValue<bool>("Retail:EnableCustomerReservations") && !expiryWorkerEnabled)
    throw new InvalidOperationException(
        "Customer reservations require Retail:EnableReservationExpiryWorker=true.");
if (expiryWorkerEnabled)
{
    var seconds = builder.Configuration.GetValue<int>("Retail:ReservationExpiry:IntervalSeconds", 30);
    var batchSize = builder.Configuration.GetValue<int>("Retail:ReservationExpiry:BatchSize", 100);
    if (seconds is < 15 or > 300 || batchSize is < 1 or > 200)
        throw new InvalidOperationException("Reservation expiry configuration is out of range.");
    builder.Services.AddHostedService<ReservationExpiryWorker>();
}
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("payfast-sandbox-itn", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("customer-onboard", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("customer-reserve", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
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

if (!builder.Environment.IsProduction() &&
    builder.Configuration.GetValue<bool>("Payments:PayfastSandbox:EnableWebhook"))
{
    builder.Services.AddScoped<IPaymentGatewayVerifier, PayfastSandboxItnVerifier>();
}

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

// Strict opt-in and never registered in a Production environment.
var sandboxSource = app.Services.GetRequiredService<PayfastTrustedSourceValidator>();
if (!app.Environment.IsProduction() &&
    app.Configuration.GetValue<bool>("Payments:PayfastSandbox:EnableWebhook") &&
    sandboxSource.IsConfigured)
{
    // Register a real verifier only in the explicitly enabled sandbox environment.
    // It will still fail closed unless the appropriate tenant merchant and payment
    // intent are independently configured and verified.
    // The sandbox verifier is DI-registered only in this guarded deployment mode.
    app.MapPost("/api/payments/payfast/sandbox/itn", async (
        HttpContext context, IWebhookReceiptProcessor receipts,
        PayfastTrustedSourceValidator source, CancellationToken ct) =>
    {
        if (!context.Request.IsHttps)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!string.Equals(context.Request.ContentType?.Split(';')[0].Trim(),
                "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (context.Request.ContentLength is > 32768 or <= 0)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        var address = context.Connection.RemoteIpAddress;
        var notification = new GatewayNotification("PayfastSandbox",
            ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(), address);
        if (!await source.IsTrustedSourceAsync(notification, ct))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var bytes = new byte[32769];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await context.Request.Body.ReadAsync(
                bytes.AsMemory(count, bytes.Length - count), ct);
            if (read == 0) break;
            count += read;
        }
        if (count == 0 || count > 32768)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        notification = notification with { Body = bytes.AsMemory(0, count) };
        var result = await receipts.ReceiveAsync(notification, ct);
        return result.Status switch
        {
            WebhookReceiptStatus.Processed or WebhookReceiptStatus.AlreadyProcessed =>
                Results.Ok(),
            WebhookReceiptStatus.Rejected => Results.BadRequest(),
            _ => Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
        };
    }).DisableAntiforgery().RequireRateLimiting("payfast-sandbox-itn");
}

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
    if (app.Configuration.GetValue<bool>("Retail:EnableCustomerOnboarding"))
    {
        app.MapPost("/api/customers/me/onboard", async (
            ClaimsPrincipal principal, ICustomerOnboarding onboarding,
            CancellationToken ct) =>
        {
            var issuer = principal.FindFirst("iss")?.Value;
            var subject = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value;
            var verified = principal.FindFirst("email_verified")?.Value;
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject) ||
                string.IsNullOrWhiteSpace(email) ||
                !string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase))
                return Results.Forbid();

            var result = await onboarding.RegisterAsync(
                new VerifiedCustomerIdentity(issuer, subject, email), ct);
            return result.Status switch
            {
                CustomerOnboardingStatus.Created => Results.Ok(new { id = result.UserId, created = true }),
                CustomerOnboardingStatus.Existing => Results.Ok(new { id = result.UserId, created = false }),
                CustomerOnboardingStatus.Invalid => Results.BadRequest(),
                _ => Results.Conflict()
            };
        }).RequireAuthorization().RequireRateLimiting("customer-onboard")
          .DisableAntiforgery();
    }

    // Explicit opt-in for authenticated sandbox checkout; never Production.
    if (!app.Environment.IsProduction() &&
        app.Configuration.GetValue<bool>("Retail:EnableCustomerReservations") &&
        expiryWorkerEnabled &&
        app.Configuration.GetValue<bool>("Payments:PayfastSandbox:EnableWebhook") &&
        app.Configuration.GetValue<bool>("Payments:PayfastSandbox:EnableCheckout") &&
        app.Configuration.GetValue<bool>("Payments:PayfastSandbox:EnableCustomerCheckout") &&
        sandboxSource.IsConfigured)
    {
        app.MapPost("/api/storefront/{storeSlug}/orders/{orderId:guid}/payfast-sandbox",
            async (string storeSlug, Guid orderId, CustomerSandboxPaymentBody body,
                ClaimsPrincipal principal, ICustomerSandboxPayment checkout,
                CancellationToken ct) =>
            {
                var issuer = principal.FindFirst("iss")?.Value;
                var subject = principal.FindFirst("sub")?.Value;
                if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
                    return Results.Forbid();

                var outcome = await checkout.StartAsync(new CustomerSandboxPaymentRequest(
                    issuer, subject, storeSlug, orderId, body.IdempotencyKey ?? ""), ct);
                return outcome.Status switch
                {
                    PayfastCheckoutStartStatus.Ready or PayfastCheckoutStartStatus.Replayed =>
                        Results.Ok(new
                        {
                            intentId = outcome.IntentId,
                            action = outcome.FormAction,
                            fields = outcome.FormFields
                        }),
                    PayfastCheckoutStartStatus.Invalid => Results.BadRequest(),
                    PayfastCheckoutStartStatus.Conflict => Results.Conflict(),
                    PayfastCheckoutStartStatus.Unavailable =>
                        Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
                    PayfastCheckoutStartStatus.Expired => Results.Conflict(),
                    _ => Results.NotFound()
                };
            })
            .RequireAuthorization()
            .RequireRateLimiting("customer-reserve")
            .DisableAntiforgery();
    }

    // Product-only reservation never accepts a client-provided physical device ID.
    if (app.Configuration.GetValue<bool>("Retail:EnableCustomerReservations"))
    {
        app.MapPost("/api/storefront/{storeSlug}/reserve-product", async (
            string storeSlug, CustomerProductReservationBody body,
            ClaimsPrincipal principal, ICustomerProductReservationService reservations,
            CancellationToken ct) =>
        {
            var issuer = principal.FindFirst("iss")?.Value;
            var subject = principal.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
                return Results.Forbid();

            var result = await reservations.ReserveOneAsync(
                new CustomerProductReserveRequest(issuer, subject, storeSlug, body.ProductId), ct);
            return result.Status switch
            {
                ReserveOrderStatus.Reserved => Results.Ok(result),
                ReserveOrderStatus.InvalidRequest => Results.BadRequest(new { error = "Invalid product." }),
                ReserveOrderStatus.OutOfStock => Results.Conflict(new { error = "Device unavailable." }),
                _ => Results.NotFound()
            };
        }).RequireAuthorization().RequireRateLimiting("customer-reserve")
          .DisableAntiforgery();
    }

    // Disabled until the automated 15-minute reservation expiry worker and
    // customer login/onboarding are configured for this deployment.
    if (app.Configuration.GetValue<bool>("Retail:EnableCustomerReservations"))
    {
        app.MapPost("/api/storefront/{storeSlug}/reserve", async (
            string storeSlug, CustomerReservationBody body, ClaimsPrincipal principal,
            ICustomerReservationService reservations, CancellationToken ct) =>
        {
            var issuer = principal.FindFirst("iss")?.Value;
            var subject = principal.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
                return Results.Forbid();

            var outcome = await reservations.ReserveAsync(new CustomerReserveRequest(
                issuer, subject, storeSlug, body.ProductId, body.DeviceId), ct);
            return outcome.Status switch
            {
                ReserveOrderStatus.Reserved => Results.Ok(outcome),
                ReserveOrderStatus.InvalidRequest => Results.BadRequest(new { error = "Invalid selection." }),
                ReserveOrderStatus.OutOfStock => Results.Conflict(new { error = "Selection unavailable." }),
                _ => Results.NotFound()
            };
        }).RequireAuthorization().RequireRateLimiting("customer-reserve")
          .DisableAntiforgery(); // Bearer tokens only; cookie auth requires CSRF protection.
    }

    app.MapGet("/api/storefront/{storeSlug}/my-orders", async (
        string storeSlug, ClaimsPrincipal principal, ICustomerOrderAccess access,
        CancellationToken ct) =>
    {
        var issuer = principal.FindFirst("iss")?.Value;
        var subject = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
            return Results.Forbid();

        // No client-provided tenant ID or user ID is accepted. Empty result
        // means no matching active external account, tenant or owned orders.
        var orders = await access.GetOwnedOrdersAsync(issuer, subject, storeSlug, 50, ct);
        return Results.Ok(orders);
    }).RequireAuthorization();

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
public sealed record CustomerReservationBody(Guid ProductId, Guid DeviceId);
public sealed record CustomerSandboxPaymentBody(string? IdempotencyKey);
public partial class Program;

public sealed record CustomerProductReservationBody(Guid ProductId);
