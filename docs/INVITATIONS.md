# Staff invitation service

## Implemented in this increment

- Cryptographically random 256-bit invitation token.
- SHA-256 hash stored in PostgreSQL, never the original token.
- Maximum seven-day expiry and allowlisted non-owner staff roles.
- Redemption must have a trusted, verified provider issuer, subject, and matching verified email.
- One-time claim with a conditional update and transactional membership insertion.
- Cross-tenant and replay tests.
- Owner roles cannot be granted by general staff invitations.
- No email-only account merging; existing provider identities match on issuer + subject.

## Intentionally not exposed

The issuance operation is **not yet an HTTP endpoint**. A configured identity provider, verified owner-approval workflow, email delivery and rate-limiting are required before invitations can be safely sent to staff.

The protected endpoint `POST /api/staff/invitations/accept` is registered only when a trusted identity authority and API audience are configured. It requires a bearer token with validated `iss`, `sub`, `email`, and `email_verified=true` claims. It accepts `{ "token": "..." }` in the JSON request body; invalid or replayed invitations return a generic error. Redemption is rate-limited to five attempts per minute per connecting IP. There is no anonymous acceptance endpoint.

We do not store tokens in logs, send them via query parameters or return them to anonymous callers.

## Next steps

- Configure a production identity authority and implement browser sign-in callback handling.
- Create an owner-authenticated invitation endpoint with a verified notification sender, audit log and resend/revoke policy.
- Add end-to-end tests for the protected invitation-acceptance endpoint with a test OIDC authority, including rejection of unverified emails.
- Add concurrency and PostgreSQL integration tests for one-use redemption.
- Add explicit account linking and role/permission policies.
