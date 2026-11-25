# Customer-owned Payfast sandbox checkout

POST /api/storefront/{storeSlug}/orders/{orderId}/payfast-sandbox

The body accepts only an idempotencyKey. Server derives tenant and customer
from validated JWT issuer/subject, active persisted external account, and
active store slug. Unknown, unowned, cross-customer and cross-tenant orders
are indistinguishable. The internal initiator independently checks order
state, expiry, device reservation and persisted price.

Strictly opt-in: registered only outside Production with a trusted configured
JWT authority/audience, Retail:EnableCustomerReservations,
Retail:EnableReservationExpiryWorker, Payments:PayfastSandbox:EnableWebhook,
Payments:PayfastSandbox:EnableCheckout,
Payments:PayfastSandbox:EnableCustomerCheckout, and configured
trusted sender ranges. Other merchant credentials and HTTPS URLs must
also be configured. The service itself checks the final opt-in.

This endpoint creates a signed Payfast sandbox hosted form; it does not
confirm payment or allocate stock. The Angular checkout is not wired yet.
Real sandbox end-to-end testing and provider verification are outstanding.
DO NOT enable real-customer payments or use this in Production.
