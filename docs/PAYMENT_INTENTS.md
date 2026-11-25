# Payment intents — no live provider yet

The backend now has a gateway-neutral payment intent record and an internal creation service.

## Guarantees
- A request is bound to a specific retailer and a particular existing order.
- Pricing and currency are obtained from the server-side order; browser totals are not trusted.
- An expired/paid/missing order cannot start a new intent.
- A caller-supplied idempotency key is scoped to the tenant and order; retries return the same intent.
- Reusing a key with a different provider is rejected.
- No card details, provider secrets or payment redirect URLs are stored.
- Only `SandboxPlaceholder` is currently accepted as a provider. This is a noncharging internal placeholder, not an integration with Payfast, Peach Payments, or any payment gateway.
- Neither intent creation nor payment confirmation is exposed as an anonymous HTTP endpoint.

## Future provider adapter requirements
1. A retailer-owned, verified merchant account and encrypted configuration per tenant.
2. Separate adapter implementation for each payment gateway, tested against sandbox documentation.
3. Server-side payment session creation with provider-returned reference and expiry.
4. Signed webhook validation, independent transaction status confirmation and merchant/amount/currency checks.
5. Idempotent event ingestion, retry queue, reconciliation, refund handling and accounting records.
6. Customer checkout permissions, fraud-risk gates, expiry and end-to-end tests.

IMPORTANT: A payment intent is **not a payment** and never authorises order fulfilment.
