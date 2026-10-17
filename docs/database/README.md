# HotelOps — Database documentation (DRAFT)

**Read this before writing database-dependent backend code.** These files are a non-executing, source-derived summary of the three uploaded SQL scripts. Their status is **provisional** and they have **not been run** or validated against the actual developer database.

## File order (when an authorized developer provisions a brand-new database)

1. `HotelOpsDB_SQLServer.sql`
2. `HotelOpsDB_Patch_v3_1_Fixed.sql` — **actual uploaded filename**, even though the comments inside SQL refer to `HotelOpsDB_Patch_v3_Final.sql`.
3. `HotelOpsDB_SampleData_Final.sql` — test data for an **empty database only**.

**Never run scripts against an existing database without verifying version and making a backup.** Note that "Final" in a filename does not mean the product/SRS permissions are formally approved.

## Reading order for Codex

1. `AGENTS.md` — repo policies and scope.
2. `docs/requirements/SRS_CHANGE_NOTES.md` — newer requirements and open decisions.
3. Relevant SRS section (`docs/requirements/HotelOps_SRS_DRAFT.md`, etc.) — old SRS pre-dates Reception.
4. `docs/database/DATABASE_RULES.md` — SQL-encoded constraints and unresolved role semantics.
5. `docs/database/DATABASE_SCHEMA.md` — tables, FK inventory, columns.
6. Actual `src/HotelOps.Api/Data/HotelOpsDbContext.cs`, `Entities`, and SQL Server schema.

## Unresolved conflicts / highest-risk areas

- Script sample already includes `RECEPTION` and three room lifecycle permissions, but these are **provisional**, while SRS change approval and revised Permission Matrix remain pending.
- Seed `DEPT_MANAGER` describes an individual department head, whereas the user-defined `Department Head` manages departments across a single branch. **Do not equate them.**
- Original SRS references Operations Manager; seed role list does not use a directly corresponding role code.
- Base `rooms.operational_status` default is `AVAILABLE`; patch changes it to `DIRTY`.
- The SQL script `HotelOpsDB_Patch_v3_1_Fixed.sql` header refers to a different filename `HotelOpsDB_Patch_v3_Final.sql` (documentation discrepancy).

## What is and is not included

- Included: derived inventory and rules; no SQL was executed; no personal account records, seeded hashes or addresses are copied into these documentation files.
- Not included: raw SQL scripts. If your team approves storing SQL, place reviewed versions in a deliberate tracked path (e.g. `database/`) rather than mixing executable seed scripts into `docs/` by accident.
- As an intentional safeguard, do not automatically regenerate the database, migration, EF scaffold, or sample records from this documentation.
