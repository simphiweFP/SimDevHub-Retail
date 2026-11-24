using Microsoft.EntityFrameworkCore;
using SimDevHub.Retail.Application;
using SimDevHub.Retail.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("RetailDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:RetailDb using environment variables or a secret manager.");

builder.Services.AddDbContext<RetailDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IStorefrontCatalogue, StorefrontCatalogueReader>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Only developer environments can opt into automatic schema updates and fictional seed data.
// Production must apply reviewed migrations independently.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Retail:SeedDemo"))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<RetailDbContext>();
    await database.Database.MigrateAsync();
    await DevelopmentSeed.SeedAsync(database);
}
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

app.Run();
public partial class Program;
