# Roadmap
1. Visual foundation: storefront, owner dashboard, responsive design, demo data.
2. Identity: tenants, branches, staff invitations, permissions, Google/Facebook OAuth.
3. Inventory: product catalogue, stock ledger, IMEI lifecycle, receiving and POS.
4. Ecommerce: cart, orders, secure gateway payment confirmation and approved BNPL.
5. Fulfilment: PAXI, Courier Guy, company vehicles, collection and tracking.
6. Procurement: suppliers, purchase orders, partial receiving, credit and returns.
7. Owner Protection: controlled refunds, discounts, discrepancies, audit and reconciliation.
8. Commercial readiness: tests, recovery, monitoring, privacy, onboarding and pilot.

## First vertical slice
Create a device; receive its IMEI; publish it; reserve it on customer order; simulate a gateway payment; allocate it once; dispatch; and track it. Add negative tests for duplicate payment callbacks and cross-tenant access.
