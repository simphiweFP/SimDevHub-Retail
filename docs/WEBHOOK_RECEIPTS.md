# Internal webhook receipt persistence

Incoming provider notifications are fingerprinted using SHA-256 and inserted
before verification. The unique (Provider, BodySha256) index deduplicates identical
provider deliveries. No raw webhook body, payer PII or merchant secret is stored.

An atomic database claim prevents two workers handling the same receipt at once;
a five-minute lease allows failed workers to be reclaimed. The attempt counter
fences late workers. At most eight claims are allowed.

Statuses: Pending, Processing, RetryPending, Completed, Rejected. Completed
receipts are idempotent. Failed verifications can be retried only when the
provider re-delivers the EXACT original payload. There is NO background replay
queue because raw payload storage and encryption are not implemented.

This is an **internal service only**. Do NOT expose a public Payfast webhook
until sandbox source validation, durable authenticated callback handling, and
merchant onboarding are complete. Receipt creation is NOT proof of payment.
