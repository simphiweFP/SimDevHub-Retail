# Sim DevHub Retail — PostgreSQL development setup

The initial EF Core migration is committed under `backend/SimDevHub.Retail.Infrastructure/Migrations/`.
The **public** storefront API filters products by active tenant and publish state. It does not serve private inventory or staff records.

## Run locally

1. Install .NET 10, Node 22, and Docker.
2. From the repository root: `docker compose -f ops/docker/compose.yml up -d`.
3. Start the backend: `dotnet run --project backend/SimDevHub.Retail.Api`.
   Development settings opt in to applying the first migration and seeding fictional `nova-mobile` products.
4. In another terminal: `cd frontend && npm install && npm start`.
5. Open `http://localhost:4200`; Angular forwards `/api` calls to `http://localhost:5082` through its dev proxy.
6. API example: `curl http://localhost:5082/api/storefront/nova-mobile/products`.

## Important boundaries

- The API-backed catalogue is real PostgreSQL read logic; all demo products are fictional. A clearly labelled fallback catalogue appears if the API is unavailable.
- Do not use sample credentials in production. Set `ConnectionStrings__RetailDb` using secure environment config or secret manager.
- Development-only `Retail:SeedDemo=true` enables automatic migration/seed; production deployment must apply reviewed migrations separately.
- Cart, payment, checkout, staff screens, OAuth, supplier purchase orders, delivery integrations and tenant-scoped administration are still prototypes.
- The screenshot-aligned hero is presentation-only and must be made dynamic for each tenant later.
- Store-specific host/subdomain routing, staff membership-based tenant authorisation and PostgreSQL integration tests are upcoming work.
