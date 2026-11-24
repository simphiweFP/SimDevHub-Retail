# Payfast sandbox ITN verification (disabled by default)

This increment adds an **internal, unregistered** `PayfastSandboxItnVerifier`
and a fixed-host sandbox server validation client.

A verified event is returned only if all of the following checks pass:
1. Payload is a bounded, valid form post with no duplicate field names
2. Merchant ID matches a credential supplied by the server, not the request
3. The notification says COMPLETE, contains a valid payment intent reference,
   a provider transaction reference and a positive two-decimal amount
4. All relevant ITN fields and the merchant passphrase generate the matching signature
5. The source validator independently trusts the transport source
6. Payfast sandbox's /eng/query/validate endpoint confirms the raw form body
7. A matching AwaitingVerification intent exists with the same amount and provider

There is **no public ITN route and no DI registration** for the verifier.
The source validation contract does not yet have a production implementation.
The merchant credentials contract also has no operational implementation.
This is deliberate: a signature alone is insufficient to confirm a payment.

Official guidance: https://developers.payfast.co.za/docs/itn-instant-transaction-notification/

## Before enabling
- Implement trusted remote IP/source validation using Payfast's documented
  source approach and a hardened proxy boundary; never trust arbitrary
  X-Forwarded-For headers.
- Add a tenant/merchant configuration resolver, backed by a secret manager.
- Implement authenticated, idempotent Payfast sandbox payment initiation so
  the provider reference and intent status cannot be forged.
- Test against a real merchant sandbox and independently reconcile statuses.
- Add a webhook receiver that safely creates GatewayNotification, responds
  to provider retries and records durable receipts.
- Complete runtime timeouts, retries, observability and security review.
