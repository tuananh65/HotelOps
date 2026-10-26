# Canonical seed manifest

| Category | Canonical content | Source / behavior |
|---|---|---|
| RBAC | 12 roles, 10 active, 62 permissions, 140 grants; zero DEPARTMENT grants | Existing release 03; upgrade uses 02 instead |
| Branches | BR001 — HotelOps Branch 1; BR002 — HotelOps Branch 2 | Explicit Task 15 decision; active; timezone Asia/Ho_Chi_Minh |
| Departments | HK, MT, WH, FO per branch, eight active rows | Explicit decision; no LAU/SEC/FB; manager_user_id remains NULL |
| PRIORITY | LOW, NORMAL, HIGH, CRITICAL | SRS GBR-085 + explicit Task 15 confirmation; URGENT rejected by canonical CHECK |
| SEVERITY | MINOR, MAJOR, CRITICAL | Existing reference contract; distinct group, unchanged meaning |
| CLEANING_TYPE | CHECKOUT, STAYOVER, DEEP_CLEAN, TURNDOWN | Observed current reference values; no invented codes |
| BLOCKER | GUEST_IN_ROOM, MISSING_SUPPLIES, EQUIPMENT_FAULT, DND | Observed current reference values |
| Users / roles / sessions | Empty personnel and session tables | Explicit decision; no old UserId 4/19 copied |
| Operational master data | Empty | Units, conversions, item categories/items, warehouses, rooms/areas, work categories and request types require approved business-specific fixtures |
| Transactional fixtures | Empty | No stock ledger, request, replenishment, incident or maintenance demo transactions |
| System settings | Empty | Current services do not require an invented settings catalog; credential policy remains in existing implementation |

The canonical data seed is transactional, locks its own resource, inserts missing records by natural keys and rejects conflicting existing data. It never resets identity values or relies on BranchId/RoleId constants. Fresh identities may currently be 1/2, but they are not part of the business contract. Seed replay must preserve all existing row values.

Both PRIORITY and SEVERITY can have a CRITICAL value because the reference key includes group_code. No permission or status code was invented.

Dependency review found that Incident and Maintenance validate active SEVERITY/PRIORITY by group and reference ID; Material Request validates active PRIORITY by ID. DTOs carry IDs rather than a fixed URGENT enum. `git grep -n -i urgent -- src tests scripts/database docs` found no tracked code/fixture reference before this package. The main DB's observed PRIORITY URGENT is a data discrepancy, not a reason to change SEVERITY. The historical SRS and Change Notes still have other unresolved role/lifecycle sections; this task does not revise them.

Personnel fixtures must be a separate, explicitly targeted test setup. See [demo fixture prerequisites](DEMO_FIXTURES.md). OM is scoped to one branch; DH has branch-wide responsibility. Future assignments must enforce active membership, branch access, expiry and SoD. Empty assignments prove no unauthorized grants, but do not test positive OM/DH workflows.
