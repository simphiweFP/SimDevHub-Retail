# Automatic reservation expiry

The application now includes an **opt-in** hosted background worker that
releases expired unpaid device reservations. It starts only when
`Retail__EnableReservationExpiryWorker=true`.

Configuration (deployment settings, not user input):

- `Retail__ReservationExpiry__IntervalSeconds`: 15–300, default 30
- `Retail__ReservationExpiry__BatchSize`: 1–200, default 100

Invalid values prevent startup. Every sweep creates a separate DI scope and
runs the existing transactional `IReservationExpiry.ReleaseExpiredAsync`
operation. The worker runs an initial sweep at startup and uses a
`PeriodicTimer` thereafter. Recoverable exceptions are logged, and later
sweeps continue. PostgreSQL conditional order updates ensure only one worker
instance can transition any order to Expired.

The customer reservation HTTP endpoint continues to require a configured
OIDC/JWT authority, plus `Retail__EnableCustomerReservations=true`.
**The API now refuses to start when customer reservations are enabled
without the expiry worker.** This prevents unattended customer holds.

Safety: reservations still expire after 15 minutes. If verified payment
arrives AFTER expiration, the payment remains for manual reconciliation
without allocating stock. Production deployment must assess the window
between hosted checkout and ITN delivery and implement reconciliation,
monitoring, customer onboarding, and fraud controls before enabling
customer checkout. No live payment capability is enabled by the worker.
