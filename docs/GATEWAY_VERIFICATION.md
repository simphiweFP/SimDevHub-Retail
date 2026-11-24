# Payment gateway verification boundary

This milestone introduces an internal `IGatewayNotificationCoordinator` and
`IPaymentGatewayVerifier`. Untrusted notifications can only reach the
verified-payment processor after a registered provider verifier returns a
validated payment event. Missing providers, duplicate provider registrations,
invalid bodies and failed verification are rejected.

**No provider verifiers are registered in the API.** There is no customer
payment callback URL and no working live payment gateway. Tests include a
test-assembly-only HMAC fixture to prove the trust boundary, not to imitate
any particular commercial gateway's signature specification.

A real provider adapter must:
1. Authenticate callback signatures using the provider's published scheme,
   reject expired timestamps, and guard against replay.
2. Independently fetch and validate transaction state, merchant ownership,
   order/intent reference, amount, currency and provider event identifier.
3. Return a `VerifiedPaymentEvent` only for a genuine completed transaction.
4. Ensure the intent was created through the provider's hosted checkout and
   transitioned from Unconfigured to AwaitingVerification legitimately.
5. Authenticate any public webhook endpoint appropriately, limit payloads,
   reject unauthenticated requests and persist event receipts for durable retries.
6. Reconcile settlement and refund events separately.

Do not register the test verifier in the application or treat browser payment
redirects as proof of payment. Manual setting of an intent to
AwaitingVerification is permitted **only in isolated automated tests**.
