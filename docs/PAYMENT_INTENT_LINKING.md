# Linking payment confirmations to payment intents

An internally verified confirmation now references a payment-intent ID in addition
to its retailer, order, provider, event ID, amount and currency.

The payment processor will **not** allocate a device unless:
- The provider event has already been independently verified by a trusted adapter.
- The intent belongs to the same tenant and order and uses the same provider.
- The intent is in `AwaitingVerification`, not `Unconfigured` or `Succeeded`.
- The order is awaiting payment and its reservation has not expired.
- Intent amount and currency match the verified provider event and order price.
- A transaction can atomically mark intent Succeeded, order Paid and device Allocated.

An event without a matching intent/provider is rejected. Late or mismatched
events for a matching intent are recorded for reconciliation but cannot fulfil.

Older payment events use a nullable `PaymentIntentId` during migration.
Live gateway callback authentication, verified merchant transaction references
and sandbox payment-session creation remain **unimplemented**.
A `SandboxPlaceholder` intent is unconfigured and can never be treated as paid.
