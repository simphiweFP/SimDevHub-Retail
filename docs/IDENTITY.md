# Identity and staff access — implementation foundation

## Implemented
- PostgreSQL user, external identity, membership and invitation entities with unique constraints.
- The membership reader resolves permissions from a **validated token issuer and subject**, never from email or a client-supplied role.
- Optional protected `GET /api/staff/{storeSlug}/me` is registered only when `Identity:Authority` and `Identity:Audience` are configured; it requires a signed, valid bearer access token.
- If identity configuration is missing, the protected route is NOT registered. There is no development bypass.
- The staff endpoint returns only store name, internal tenant ID and role for an active user, active membership and active retailer.
- A query-level test covers cross-store access, wrong subjects and account deactivation.

## Not implemented yet
- End-user Google and Facebook OAuth redirects/callback handling.
- Identity-provider account linking and consent.
- Staff invitation creation/acceptance, email confirmation and MFA.
- Role-based endpoint policies beyond verifying active membership.
- Browser session handling, logout, refresh tokens and CSRF considerations.

## Provider configuration
The configuration values are `Identity__Authority` and `Identity__Audience` via environment variables. They must identify a trusted HTTPS issuer and API audience. Do not point these settings at Google ID tokens or Facebook access tokens indiscriminately. The platform needs a deliberate OIDC identity architecture for external login before it can authenticate browsers. Provider secrets must remain out of Git.

## Trust boundaries
- `{storeSlug}` is a lookup target, not an authority to access records. Membership is determined server-side.
- The JWT signature, lifetime, issuer and audience are validated before membership resolution.
- An email address or matching domain must NEVER grant staff membership.
- Invites are stored as hashes, not plaintext; signing and email delivery workflow still need implementation.
- Tenant role and subscription status must be checked on every private operation as the product grows.
