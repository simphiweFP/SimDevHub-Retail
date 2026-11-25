# Order reservation vertical slice

## Implemented
- Internal service creates a 15-minute AwaitingPayment order for one serialised device.
- Unit price is read from the tenant's published catalogue record, not from a browser field.
- Atomic PostgreSQL conditional update changes device Available -> Reserved.
- Order insertion and Reserved stock movement commit in the same database transaction.
- Two parallel reservations cannot claim the same device; a PostgreSQL integration test checks the result.
- Cross-tenant requests cannot reserve another retailer's product or IMEI.

## Not implemented
- This is deliberately NOT a checkout HTTP endpoint.
- No real customer identity, carts with multiple lines, delivery fees, tax calculations, payment providers or refunds.
- Reservation expiry/release is now implemented as an internal service, but a scheduled worker is **not yet registered**; expired stock is only released when that service is invoked.
- No simulated or real payment-success transition is implemented.
- Real IMEI checksum and manufacturer serial-number alternatives still need integration.

## Next steps
- Schedule reservation expiry using a reliable worker (database-backed jobs), and integrate operator monitoring.
- Add idempotent order payment-state transitions using signed, verified test webhooks.
- Use customer-authenticated checkout endpoints with stable idempotency keys and account protection.
- Introduce a proper OrderLine model for multi-item carts and quantity-tracked accessories.
