# Payfast sandbox webhook (strict opt-in, NOT enabled by default)

The API has a sandbox-only, HTTPS-only, direct-network-peer-validated webhook.
It is registered only when **all** conditions are met:
- ASPNETCORE_ENVIRONMENT is Development or Staging (never Production)
- `Payments__PayfastSandbox__EnableWebhook=true` is explicitly set
- `Payments__PayfastSandbox__TrustedSourceCidrs` contains a valid narrow IP/CIDR list

The callback route is `POST /api/payments/payfast/sandbox/itn`. It accepts only
`application/x-www-form-urlencoded`, at most 32 KiB, over HTTPS. A request
must originate from a configured Payfast network peer; X-Forwarded-For and
Referer are NOT trusted. Invalid source and invalid payments are rejected.

The processor records a webhook receipt fingerprint before attempting the
verified gateway coordinator. This is still NOT a full automatic retry queue:
provider re-delivery is required. Duplicate completed requests return 200.
Temporary failures return 503 so the provider may retry; permanent
rejections return 400. No card/payment secrets enter logs or browser bundles.

**Reverse proxy restriction:** Do not enable this route behind a reverse proxy,
load balancer or CDN without first implementing a verified trusted-proxy
topology and correctly obtaining the original sender address. This endpoint
uses `HttpContext.Connection.RemoteIpAddress` only. A proxy's IP is
not a Payfast source IP and must not be added to the allowlist to work around
the check. Disable or block requests with spoofable forwarded headers.

Populate the published Payfast IP ranges from the official documentation
after verifying their current applicability to your sandbox environment:
https://developers.payfast.co.za/documentation/

Merchant credentials are tenant-scoped and loaded from a secret manager.
A matching PayfastSandbox payment intent must already have been created
through an authenticated Payfast hosted-checkout flow. That flow is NOT yet
implemented in this repository, so real paid orders remain unavailable.
Do not use manual database state changes in production to activate intents.

The source list, ITN signature procedure and Payfast validation endpoint
must be rechecked during real sandbox UAT before this feature is enabled.
