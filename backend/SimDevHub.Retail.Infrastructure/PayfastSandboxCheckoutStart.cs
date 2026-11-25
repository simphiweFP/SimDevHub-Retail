using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Domain;

namespace SimDevHub.Retail.Infrastructure;

/// <summary>
/// Creates a sandbox hosted-checkout POST form, backed by exactly one
/// PayfastSandbox intent for a reserved order. Never initiates live payments.
/// Not exposed as HTTP until customer authentication and ownership exist.
/// </summary>
public sealed class PayfastSandboxCheckoutStart(
    RetailDbContext db, IPayfastSandboxMerchant merchants,
    PayfastTrustedSourceValidator source, IConfiguration config, TimeProvider clock)
    : IPayfastSandboxCheckoutStart
{
    private const string Provider = "PayfastSandbox";

    public async Task<PayfastCheckoutStartResult> StartAsync(
        StartPayfastSandboxCheckout request, CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty || request.OrderId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.IdempotencyKey.Length is < 8 or > 100 ||
            !request.IdempotencyKey.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            return new(PayfastCheckoutStartStatus.Invalid);

        // Both switches and a narrow source allowlist are mandatory. The ITN
        // callback cannot receive confirmations without this configuration.
        if (!string.Equals(config["Payments:PayfastSandbox:EnableCheckout"], "true", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(config["Payments:PayfastSandbox:EnableWebhook"], "true", StringComparison.OrdinalIgnoreCase) ||
            !source.IsConfigured)
            return new(PayfastCheckoutStartStatus.Unavailable);

        var credentials = await merchants.GetCredentialsAsync(request.TenantId, ct);
        if (credentials is null || string.IsNullOrWhiteSpace(credentials.MerchantKey))
            return new(PayfastCheckoutStartStatus.Unavailable);

        var returnRaw = config["Payments:PayfastSandbox:ReturnUrl"];
        var cancelRaw = config["Payments:PayfastSandbox:CancelUrl"];
        var notifyRaw = config["Payments:PayfastSandbox:NotifyUrl"];
        if (!Uri.TryCreate(returnRaw, UriKind.Absolute, out var returnUrl) ||
            !Uri.TryCreate(cancelRaw, UriKind.Absolute, out var cancelUrl) ||
            !Uri.TryCreate(notifyRaw, UriKind.Absolute, out var notifyUrl) ||
            notifyUrl.AbsolutePath != "/api/payments/payfast/sandbox/itn" ||
            !string.IsNullOrEmpty(notifyUrl.Query))
            return new(PayfastCheckoutStartStatus.Unavailable);

        var order = await db.RetailOrders.AsNoTracking()
            .Where(x => x.Id == request.OrderId && x.TenantId == request.TenantId &&
                        x.Tenant.IsActive && x.Device.TenantId == x.TenantId)
            .Select(x => new {
                x.Status, x.UnitPrice, x.ReservedUntil, DeviceStatus = x.Device.Status,
                ProductName = x.Product.Name
            }).SingleOrDefaultAsync(ct);
        if (order is null || order.Status != "AwaitingPayment" || order.DeviceStatus != "Reserved")
            return new(PayfastCheckoutStartStatus.OrderUnavailable);
        if (order.ReservedUntil <= clock.GetUtcNow())
            return new(PayfastCheckoutStartStatus.Expired);
        if (order.UnitPrice <= 0 || order.UnitPrice > 100000000m)
            return new(PayfastCheckoutStartStatus.Invalid);

        // Exactly one PayfastSandbox intent per order, protected by the DB index.
        var existing = await db.PaymentIntents.AsNoTracking()
            .Where(x => x.OrderId == request.OrderId && x.TenantId == request.TenantId &&
                        x.Provider == Provider)
            .Select(x => new { x.Id, x.IdempotencyKey, x.Amount, x.Currency, x.Status })
            .SingleOrDefaultAsync(ct);
        if (existing is not null)
        {
            if (existing.IdempotencyKey != request.IdempotencyKey ||
                existing.Amount != order.UnitPrice || existing.Currency != "ZAR" ||
                existing.Status != "AwaitingVerification")
                return new(PayfastCheckoutStartStatus.Conflict);
            return BuildForm(existing.Id, existing.Amount, order.ProductName,
                credentials, returnUrl, cancelUrl, notifyUrl, PayfastCheckoutStartStatus.Replayed);
        }

        var id = Guid.NewGuid();
        var generated = BuildForm(id, order.UnitPrice, order.ProductName,
            credentials, returnUrl, cancelUrl, notifyUrl, PayfastCheckoutStartStatus.Ready);
        if (generated.Status != PayfastCheckoutStartStatus.Ready) return generated;

        db.PaymentIntents.Add(new PaymentIntent
        {
            Id = id, TenantId = request.TenantId, OrderId = request.OrderId,
            IdempotencyKey = request.IdempotencyKey, Provider = Provider,
            Status = "AwaitingVerification", Currency = "ZAR",
            Amount = order.UnitPrice, CreatedAt = clock.GetUtcNow()
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return generated;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            var winner = await db.PaymentIntents.AsNoTracking()
                .Where(x => x.OrderId == request.OrderId &&
                            x.TenantId == request.TenantId && x.Provider == Provider)
                .Select(x => new { x.Id, x.IdempotencyKey, x.Amount, x.Currency, x.Status })
                .SingleOrDefaultAsync(ct);
            return winner is not null && winner.IdempotencyKey == request.IdempotencyKey &&
                   winner.Amount == order.UnitPrice && winner.Currency == "ZAR" &&
                   winner.Status == "AwaitingVerification"
                ? BuildForm(winner.Id, winner.Amount, order.ProductName,
                    credentials, returnUrl, cancelUrl, notifyUrl, PayfastCheckoutStartStatus.Replayed)
                : new(PayfastCheckoutStartStatus.Conflict);
        }
    }

    private static PayfastCheckoutStartResult BuildForm(
        Guid id, decimal amount, string productName, PayfastMerchantSettings merchant,
        Uri returnUrl, Uri cancelUrl, Uri notifyUrl, PayfastCheckoutStartStatus successStatus)
    {
        try
        {
            var credentials = new PayfastSandboxCheckoutBuilder.Credentials(
                merchant.MerchantId, merchant.MerchantKey ?? "", merchant.Passphrase);
            var form = PayfastSandboxCheckoutBuilder.Build(credentials, new(
                id.ToString("N"), amount, productName,
                returnUrl, cancelUrl, notifyUrl));
            return new(successStatus, id, form.Action.AbsoluteUri, form.Fields);
        }
        catch (ArgumentException)
        {
            return new(PayfastCheckoutStartStatus.Unavailable);
        }
    }
}
