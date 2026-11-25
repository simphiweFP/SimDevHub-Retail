# Tenant-specific Payfast sandbox merchant configuration

This module adds `ConfiguredPayfastSandboxMerchant`, a fail-closed,
tenant-keyed configuration resolver. The resolver does **not** enable payments,
ITN reception, a public webhook route or a merchant onboarding API.

## Configuration (secrets must NOT be committed)

For a retailer whose database tenant ID is `01234567-89ab-cdef-0123-456789abcdef`,
configure the following keys via a secure deployment secret store:

```text
Payments__PayfastSandbox__Merchants__0123456789abcdef0123456789abcdef__Enabled=true
Payments__PayfastSandbox__Merchants__0123456789abcdef0123456789abcdef__MerchantId=<your sandbox merchant id>
Payments__PayfastSandbox__Merchants__0123456789abcdef0123456789abcdef__Passphrase=<your sandbox passphrase>
```

The tenant ID format is the canonical GUID **without hyphens**, matching
`Guid.ToString("N")`. Do not use another retailer's credentials. An
`Enabled=true` value is mandatory; missing or malformed credentials fail
closed. There are deliberately no global merchant credentials or shared
fallback. No credentials should be included in Git, documentation examples,
logs, frontend bundles or error responses.

## Runtime status

- The resolver can be used only through an explicitly registered
  `IPayfastSandboxMerchant` service. It does not initiate a payment.
- Payfast ITN verification remains unregistered until trusted source
  validation, actual sandbox onboarding and webhook durable processing exist.
- A future tenant onboarding workflow must verify that each merchant account
  belongs to its intended retailer.
- Prefer a dedicated secret manager with access controls and rotation support
  over plaintext deployment environment variables where possible.

## Next

Implement a hardened trusted-source check, documented sandbox ITN callback
handler, webhook receipt persistence, retries and sandbox UAT.
