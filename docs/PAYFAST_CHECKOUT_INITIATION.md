# Payfast sandbox payment initiation — internal only

`IPayfastSandboxCheckoutStart` now creates a payment intent for an existing,
active-tenant, unexpired reserved order. It uses the **database order price**
and product name, not client-supplied amounts. The Payfast form's
`m_payment_id` is the payment-intent GUID (N format).

### Explicit deployment prerequisites

- `Payments__PayfastSandbox__EnableCheckout=true`
- `Payments__PayfastSandbox__EnableWebhook=true`
- `Payments__PayfastSandbox__TrustedSourceCidrs` configured with reviewed
  narrow Payfast IP ranges (direct TLS network peers only)
- `Payments__PayfastSandbox__ReturnUrl`, `CancelUrl`, and `NotifyUrl`
  configured as HTTPS URLs; NotifyUrl must end exactly in
  `/api/payments/payfast/sandbox/itn`
- The tenant's `Payments__PayfastSandbox__Merchants__<tenant-guid-N>`
  must be explicitly Enabled and include MerchantId, MerchantKey, Passphrase.
  Use a secret manager; never commit credentials.

Each reserved order can have only one PayfastSandbox payment intent.
Same-key retry rebuilds the same form; a conflicting key is rejected.
The unique filtered PostgreSQL index also guards concurrent creation.
Expired reservations and non-reserved devices cannot create a new form.

**Important boundaries:** No customer checkout HTTP route calls this service.
No user identity or order ownership checks are implemented in this internal
service. Never expose it anonymously; a future authenticated customer
controller must prove order ownership from a trusted identity. A form
submission begins a Payfast sandbox payment; it does not establish settlement.
Only independently verified ITNs may allocate stock.

This has not been tested with a real Payfast sandbox merchant account.
Production/live payments are not supported.
