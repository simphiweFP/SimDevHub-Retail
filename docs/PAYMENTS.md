# Verified payment transaction foundation

Implemented:
- Provider-event records, immutable event identifiers, database unique constraint and deduplication.
- A strictly **internal** verified-payment processor.
- Conditional order AwaitingPayment -> Paid transition, before reservation deadline and only for exact ZAR order amount.
- Atomic device Reserved -> Allocated transition and inventory movement in the same transaction.
- Late payments, amount mismatches and already fulfilled orders are recorded as ReviewRequired.
- A provider retry cannot allocate the handset twice.

Explicitly NOT implemented:
- Gateway integration, webhook signature verification, merchant credentials or merchant onboarding.
- HTTP payment confirmation endpoints, customer checkout, payment intents, refunds, settlements or provider reconciliation.
- Fully automated payments to suppliers.

A browser redirect or user-supplied 'paid' flag must **never** be passed to the verified-payment processor. Only a trusted adapter which verifies the payment with the provider may invoke it.

Next: payment intent and sandbox gateway adapters, reliable outbox, refund/reconciliation cases, fulfilment status transitions and multi-item order support.
