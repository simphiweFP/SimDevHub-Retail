# Payfast sandbox hosted checkout form — isolated builder

A sandbox-only form builder is now implemented in
`backend/SimDevHub.Retail.Infrastructure/PayfastSandboxCheckoutBuilder.cs`.

It follows Payfast's documented hosted payment form POST:
- `https://sandbox.payfast.co.za/eng/process` (never live endpoint)
- merchant ID/key, HTTPS return/cancel/notify URLs, m_payment_id,
  ZAR amount formatted with exactly two decimals, item name, and MD5 signature
- signature is generated server-side from the documented ordered fields and
  secure merchant passphrase
- the passphrase is not included in the form fields

Official references:
- https://developers.payfast.co.za/docs
- https://github.com/PayFast/payfast-php-sdk/blob/master/lib/Auth.php

**Security and operational constraints**
- This is a pure builder, NOT a payment gateway implementation. No API endpoint
  exposes it and no merchant credentials are committed to the repository.
- Credentials in automated tests are fictional values, not usable merchant secrets.
- No Payfast ITN verifier has been implemented or registered. The signature on
  a checkout form is NOT evidence of successful payment.
- Do not transition a payment intent to AwaitingVerification or mark an order
  paid until merchant-scoped sandbox credentials, a registered ITN receiver,
  signature validation, source/transaction verification, and reconciliation
  have been fully implemented and tested.
- The currently shipped `SandboxPlaceholder` payment-intent provider remains
  unconfigured and cannot accept a real charge.
- Real multi-tenant checkout must resolve merchant credentials from the
  server-side tenant membership and never from a browser.
- Payfast may update integration requirements; cross-check the official
  documentation and run account-specific sandbox/UAT before enabling a live
  merchant.
