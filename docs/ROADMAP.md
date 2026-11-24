# Sim DevHub Retail roadmap

## Current prototype
- Customer-facing storefront, product search and category filters (demo catalogue).
- Shared in-memory shopping basket with quantity changes.
- Responsive checkout preview with delivery choices (Courier Guy, PAXI, store fleet, store pickup) and planned payment choices including instalments.
- Owner overview with sample sales, inventory and audit data.
- .NET 10 demo catalogue API.

**Not operational yet:** payment processing, supplier purchasing, stock reconciliation, real delivery orders, external sign-in, tenant isolation and PostgreSQL. The checkout never takes a payment or creates a real order.

## Phases
1. Pixel-reviewed reference fidelity: product imagery, storefront, admin layouts, mobile QA and accessibility.
2. Identity: tenant and branch model, staff invitation, MFA, Google/Facebook sign-in.
3. Inventory: product catalogue, IMEI lifecycle, receiving and POS.
4. Ecommerce: real cart/order persistence, secure gateway payments, BNPL enabled by merchant provider.
5. Delivery: PAXI, Courier Guy, own fleet and pickup; tracking and POD.
6. Procurement: suppliers, POs, credit balances, partial receipts and returns.
7. Owner protection: approvals, audit, reconciliation, fraud-risk alerts.
8. Commercial pilot: hardening, backups, privacy review, deployment and UAT.

## Next vertical slice
Create a unique physical device -> receive into tenant stock -> publish product -> reserve stock -> create pending order -> simulate verified gateway callback -> allocate the device once -> dispatch -> view tracking. Include cross-tenant denial and duplicate-webhook tests.
