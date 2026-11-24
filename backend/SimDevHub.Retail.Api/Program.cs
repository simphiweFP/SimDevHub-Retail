using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("RetailDb")
    ?? throw new InvalidOperationException("ConnectionStrings:RetailDb must be configured. Do not commit credentials.");

builder.Services.AddDbContext<RetailDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IStorefrontCatalogue, StorefrontCatalogueReader>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

// Public storefront endpoint: returns only published products for one active tenant.
// Do not use this route to return staff, pricing costs, inventory IMEIs or private business records.
app.MapGet("/api/storefront/{storeSlug}/products", async (
    string storeSlug, IStorefrontCatalogue catalogue, CancellationToken cancellationToken) =>
{
    if (storeSlug.Length is < 2 or > 80 ||
        !storeSlug.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
        return Results.BadRequest(new { error = "Invalid store slug." });

    var store = await catalogue.GetPublishedAsync(storeSlug, cancellationToken);
    return store is null ? Results.NotFound() : Results.Ok(store);
})
.WithName("GetStorefrontProducts");

app.Run();

public partial class Program;
