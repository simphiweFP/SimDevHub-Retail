using SimDevHub.Retail.Application;
using SimDevHub.Retail.Infrastructure;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
builder.Services.AddScoped<IProductReader,DevelopmentProductReader>();
var app=builder.Build();
app.MapHealthChecks("/health");
// Demo-only endpoint. Replace with authenticated tenant-scoped catalogue before production.
app.MapGet("/api/demo/products",async (IProductReader reader,CancellationToken ct) =>
  Results.Ok(await reader.GetPublishedAsync(Guid.Empty,ct)));
app.Run();
