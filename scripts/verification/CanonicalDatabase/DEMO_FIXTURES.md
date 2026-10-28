# Separate personnel and operational fixtures

Canonical setup intentionally creates no login account. A successful schema/seed run therefore does not make the authenticated application immediately usable.

Before positive API tests, prepare a separate fixture package approved for an independent database. Identify the users by fixture-specific natural keys; create their required branch/department relationships; hash a runtime-generated password with the existing `IPasswordCredentialService` / PasswordCredentialService implementation; and supply the initial credential through a local secret channel. Do not commit plaintext, password hashes, session material, temporary credentials or an administrator account into this package. Do not reuse old user IDs or copy production/developer sessions. This document specifies prerequisites; no personnel provisioning command is supplied or claimed tested.

Assign only the permissions/branch roles required by the scenario, create `user_branch_access`, and respect user/role activity and assignment expiry. Choose distinct requester/approver and performer/verifier identities. OM/DH/Storekeeper assignments require the agreed role/branch/SoD checks; no broad SYS_ADMIN shortcut is supplied.

For Inventory/API fixtures, provision approved UOM/category/item/warehouse data first. Material Request additionally needs a requester, approver, receiving location where applicable, and valid item/stock lineage. Replenishment and Maintenance require their source records and actor identities. Existing integration runners are not automatically reused because they contain machine/main-database guards; do not disable those guards or redirect credentials blindly.

The optional EF persistence probe needs no users and uses one uniquely named UOM inside a rollback-only trial transaction. It verifies generated rowversion and a stale-write conflict, without implementing or testing business workflow/authorization. Identity/rowversion counters may advance on the disposable database even after rollback; canonical rows and fixture counts are preserved.
