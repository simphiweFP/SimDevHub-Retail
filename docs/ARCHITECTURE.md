# Architecture decisions

- Angular standalone-component storefront and owner dashboard.
- ASP.NET Core .NET 10 modular monolith; Domain, Application, Infrastructure, API separation.
- PostgreSQL with EF Core planned; no production database connection in this scaffold.
- Tenant-aware permissions must be enforced server-side before any merchant data API is exposed.
- Hosted payment gateway integration only; no card credentials stored in Sim DevHub.
- Google/Facebook sign-in requires real OAuth registration, secure callbacks and staff invitation policies.
- Inventory ledger will support uniquely identified IMEI/serialised products and quantity-tracked accessories.
- Purchasing, supplier returns, BNPL, delivery tracking and Owner Protection Centre are planned modules.

## Security gate
No real customer payments, stock data, or production sign-ins should be used with demo endpoints or placeholder UI.
