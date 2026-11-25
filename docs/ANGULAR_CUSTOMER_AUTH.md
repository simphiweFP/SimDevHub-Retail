# Angular customer authentication — opt-in

The Angular storefront now supports an OIDC authorization-code + PKCE flow
using `oidc-client-ts`. The library handles OAuth state, nonce and PKCE.
It never accepts a password, client secret or arbitrary redirect URL.

**No identity provider is configured in Git.** Deploy
`/auth-config.json` **on the same origin as the Angular app** containing
public client settings ONLY, for example:

```json
{
  "enabled": true,
  "authority": "https://identity.example.com",
  "clientId": "retail-spa-client",
  "scope": "openid profile email retail.api"
}
```

These are example placeholders, not a working tenant or an OIDC registration.
The authority must use HTTPS. Register redirect URI
`https://YOUR-STORE-ORIGIN/auth/callback` as an allowed callback for the
public SPA client. The API must independently validate issuer, signing keys,
audience and token lifetime; configure `Identity__Authority` and
`Identity__Audience` accordingly. Ensure the requested API scope generates
an ACCESS token specifically intended for the retail API. The browser does
not send an ID token to the API.

The sign-in screen remains disabled when `/auth-config.json` is absent or
disabled. `/auth/callback` validates the redirect using the OIDC library.
`/account` invokes POST `/api/customers/me/onboard` (if enabled) and then
GET `/api/storefront/nova-mobile/my-orders` with the access token as a bearer
header. API errors are not presented as successful onboarding. Sessions use
sessionStorage, avoiding cross-tab persistent tokens, but XSS remains a risk;
maintain a strong CSP and avoid third-party scripts.

The customer onboarding API itself remains explicitly opt-in
(`Retail__EnableCustomerOnboarding=true`). Account registration requires
the validated JWT to contain `email_verified=true`. This still needs real
OIDC provider registration, browser/API integration testing, token expiry
UX, refresh strategy, account recovery and logout end-session behaviour.

The storefront tenant slug is still a fixed **demo** slug. Production
tenant routing/host-to-store binding must precede real multi-store rollout.

No payment button is wired. All payments remain disabled.
