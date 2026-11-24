# Payment exception queue

The internal read model lists verified provider events that could not safely
advance their order and were recorded as `ReviewRequired`. Typical cases are
amount mismatches, expired reservations and payments against already completed orders.

The query is explicitly filtered by tenant ID and capped at 100 records.
An authenticated read-only route is available when the trusted JWT authority is configured:
`GET /api/staff/{storeSlug}/payment-exceptions`.
It resolves tenant identity exclusively from the validated issuer/subject and active
staff membership, then permits only `Owner`, `Manager` or `FinanceOfficer`.
The endpoint returns at most 50 exception records; there is no public endpoint,
and there is still no write/approval route.

This is an **exception list**, not a general ledger or proof of bank settlement.
Resolution, refund approval and independent provider reconciliation will be
implemented in subsequent phases. Do not change stock simply because a staff
member dismisses a payment exception.
