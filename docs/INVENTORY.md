# Serialised inventory foundation

## Implemented
- One physical record per phone IMEI, separate from the public catalogue product.
- Tenant + IMEI unique database constraint; rejects duplicate device registrations.
- Receiving service checks that the product belongs to the specified active tenant.
- Every accepted receipt records an inventory movement and reference.
- PostgreSQL integration test for tenant/product mismatch, invalid IMEI, duplicate IMEI and movement insertion.

## Not implemented
- Staff-authorised goods-receiving HTTP endpoints, audit actor attribution and purchasing-order matching.
- Reservations, sales, refunds, stock transfers, returns and IMEI scan UI.
- Checksum verification and exception handling for devices without standard IMEI.
- Supplier ownership/consignment logic and bulk accessories inventory.

## Security boundary
InventoryReceivingService is deliberately internal. Before exposing it, resolve tenant and branch from authenticated staff membership, require receiving permission, add business document checks and audit actor attribution.

Never display raw IMEIs in public storefront or tracking pages.
