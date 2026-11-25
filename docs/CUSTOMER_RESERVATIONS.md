# Authenticated, customer-owned reservations

The internal `CustomerReservationService` resolves an active buyer through
an existing trusted OIDC issuer+subject link in `external_accounts`, and
resolves an active retailer via its store slug. It never accepts tenant ID or
customer ID from an HTTP request.

`OrderReservationService` now optionally stamps CustomerUserId into the
retail order in the same transaction as the conditional device reservation,
inventory movement and server-priced order insertion. Legacy internal
reservations without an owner remain supported and unclaimable via the
customer history/ownership API.

The JWT-authenticated `POST /api/storefront/{storeSlug}/reserve` accepts
only ProductId and DeviceId; it is registered ONLY when both a valid OIDC
authority is configured and `Retail__EnableCustomerReservations=true`.
The default is disabled. Per-network-peer rate limiting is 3 per minute;
the endpoint does NOT support anonymous reservation, and rejects identities
that lack an active persisted external account.

IMPORTANT: do not enable this endpoint in production until a trusted
customer onboarding flow, reservation expiration background worker,
fraud/abuse controls, and order checkout orchestration are configured.
Existing expiry is internal-only and will not automatically release holds.

The endpoint creates a reservation only; it does not start or collect
payment or expose Payfast merchant credentials. The current device selection
uses an internal DeviceId; a future storefront stock-selection API must avoid
exposing internal serial/IMEI identifiers.

## Product-only Angular reservation (opt-in)

Authenticated customers may POST `/api/storefront/{storeSlug}/reserve-product` with
`{ "productId": "<catalogue UUID>" }`. The backend resolves the active
store and persisted OIDC customer, selects an available serialised device,
and reuses the existing conditional, transactional inventory reservation.
The response contains order ID, backend price and expiry but **never IMEI**.
Concurrent buyers cannot allocate the same device. The selection is limited
to one device and may return 409 when stock is unavailable.

The Angular checkout only exposes this when its optional public
`/auth-config.json` sets `enableReservations: true`, the customer is
signed in and exactly one actual UUID catalogue item is in the basket.
The API separately requires `Retail:EnableCustomerReservations=true` and
the reservation-expiry worker (required during startup). Never use the
browser basket subtotal as an authoritative charge. Multi-item carts,
delivery fees, discounts, tax, live merchant checkout and refunds remain
out of scope. Provider payments are independent, and no successful payment
is inferred from creating a reservation.
