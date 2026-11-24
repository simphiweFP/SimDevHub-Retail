# Payment exception queue

The internal read model lists verified provider events that could not safely
advance their order and were recorded as `ReviewRequired`. Typical cases are
amount mismatches, expired reservations and payments against already completed orders.

The query is explicitly filtered by tenant ID and capped at 100 records.
No route is available until authentication, staff membership, role authorisation
and appropriate audit/rate-limit protections have been applied.

This is an **exception list**, not a general ledger or proof of bank settlement.
Resolution, refund approval and independent provider reconciliation will be
implemented in subsequent phases. Do not change stock simply because a staff
member dismisses a payment exception.
