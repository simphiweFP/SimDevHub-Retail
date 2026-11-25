# Customer order ownership — backend foundation

Orders now have a nullable `CustomerUserId` FK to the stable `RetailUser`.
Historical/internal orders remain unowned. No public claim-an-order function
is provided; an email address or an arbitrary GUID does not prove ownership.

`CustomerOrderAccess` resolves the user through an *existing*, active
`ExternalAccount` whose issuer and subject match the validated JWT.
All queries require an active tenant and, for order history, an active user.
The read-only `GET /api/storefront/{storeSlug}/my-orders` is registered
only if a trusted JWT authority is configured; it requires authorization.
It returns only the matching user's orders for that store, and never returns
IMEI, internal device IDs, merchant settings or payment details.

There is NO customer account creation/binding flow yet and NO publicly
exposed order reservation or checkout initiation endpoint. The internal
`OwnsOrderAsync` check is intended for future authenticated checkout
routes; until customer-bound reservations exist it will return false for
all legacy internal reservations.

Before enabling customer checkout, provision and verify customer external
identities, bind ownership atomically while creating a reservation, and
use OwnsOrderAsync before initiating Payfast checkout.
