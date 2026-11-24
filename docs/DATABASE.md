# Database foundation — first real persistence increment

This increment replaces the hardcoded **backend catalogue** with a PostgreSQL-ready, tenant-filtered public storefront query. The Angular storefront still uses local demo catalogue items; wiring it to this API is a following step.

## Development startup

1. Run `docker compose -f ops/docker/compose.yml up -d` from the repository root.
2. Install .NET 10 SDK and EF Core CLI (`dotnet tool install --global dotnet-ef --version 10.0.0`).
3. Generate the initial migration (only once): `dotnet ef migrations add InitialRetailCatalogue --project backend/SimDevHub.Retail.Infrastructure --startup-project backend/SimDevHub.Retail.Api --output-dir Migrations`.
4. Review the generated migration and commit it before production deployment.
5. Apply the migration locally: `dotnet ef database update --project backend/SimDevHub.Retail.Infrastructure --startup-project backend/SimDevHub.Retail.Api`.
6. Run `dotnet run --project backend/SimDevHub.Retail.Api`.

## Public API

`GET /api/storefront/{storeSlug}/products` returns a public store name, slug, and **published** products for an **active** tenant, or 404. It never returns IMEIs or private stock information. This is NOT an authenticated staff API and does not establish private tenant authorisation.

## Next required security and functionality steps

- Store product management APIs must be authenticated and derive tenant identity from server-issued staff membership, not client-supplied IDs.
- Add production migrations (the initial migration has not been generated in this increment).
- Configure secrets via environment / secret manager. The development credentials in docker compose must never be reused outside local development.
- Add staff identity tables, verified Google/Facebook OAuth and staff invitation policies.
- Integrate the Angular storefront against the catalogue endpoint and add production data seeding/onboarding.
- Add PostgreSQL integration tests beyond the SQLite isolated query test.

No customer payment or production authentication is enabled.
