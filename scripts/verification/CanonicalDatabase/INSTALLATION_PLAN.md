# Installation plan

## Prerequisites and guards

- .NET 10, SQL Server 2022+, existing EF packages; Integrated Security on the reviewed instance `DESKTOP-JI8U50T\SQLEXPRESS`.
- `Run-Canonical.ps1` checks the server response and selected database on connection. Targets must match `HotelOps_Canonical_<UTC timestamp>_<8 hex>_{Fresh,Upgrade,AuthUpgrade}`. Arbitrary names and `HotelOpsDB` are rejected.
- Review SQL creation privileges separately. The trial account happened to be sysadmin; this does not prove least-privilege application access. The package grants no permissions.
- Original source bytes remain unchanged. In-memory derived copies substitute the explicit trial database for `HotelOpsDB` / the upgrade placeholder, including each corresponding guard. Guards are retained; no least-privilege guard is disabled.
- Every SQL batch stops on the first exception. A failure may leave an independently created, partially installed trial database. Retain it and its error evidence; do not apply the full installer again or overwrite it.

## Fresh installation

1. Confirm all three trial names are absent before any CREATE DATABASE.
2. Execute derived `01_HotelOps_Full_Schema.sql`: creates the independent base schema (72 tables), including Authentication columns and RBAC structure.
3. Execute derived `03_HotelOps_RBAC_Catalog_Seed.sql` exactly once for this fresh schema.
4. Apply DEV2 contracts in order: Inventory `20261019`, Material Request `20261020`, Replenishment `20261021`, Maintenance Material Usage `20261022`.
5. Apply canonical `00_Canonical_Reference_Contract.sql`, then `01_Canonical_Seed.sql`. Replay the data seed and compare complete branch/department/reference rows, including IDs and timestamps.
6. Execute release `04` and canonical `02_Canonical_Verify.sql`, EF probe, CHECKDB and CHECKCONSTRAINTS.

## Upgrade from an existing Authentication/RBAC database

Never use Full Schema on an existing database. First review and back up the source, then restore to a new isolated name with separately reviewed file paths; this task performs no backup/restore and does not offer an automatic main-database upgrade command.

- **Authentication-ready, pre-canonical RBAC:** verify the three credential lifecycle columns and `session_key_hash`. Review role aliases, personnel/SoD conflicts and any `@SelectedDh` selections on that exact clone, then run a derived, reviewed `02_HotelOps_Upgrade_Auth_To_RBAC.sql`. Its `data_scope` pre-step is outside the main catalog transaction and can survive a later failure. Do not run `03` afterward.
- **Already canonical RBAC:** verify exact catalog and assignment state; skip both `02` and `03`. Do not normalize an already canonical catalog again.
- Inventory existing DEV2 tables/columns/indexes/constraints before applying any patch. On a known unpatched base, apply the four patches in the fresh order. On a partially patched database, stop and compare definitions: FE-10 and Material Usage deliberately reject existing tables; FE-09's column grouping assumes a particular starting state. The package does not guess missing deltas.
- Existing branches outside BR001/BR002 or conflicting departments cause the canonical seed to stop. Existing noncanonical PRIORITY values cause the reference contract to stop. Mapping a real clone's references/personnel requires separate review; no automatic deletion, renaming or reassignment is included.

The automated `Trial` command exercises these paths on three **new synthetic databases**. `_Upgrade` is a populated-RBAC catalog base with no personnel/business data. `_AuthUpgrade` starts from the base Auth schema plus controlled legacy role aliases and empty grants, then executes `02`. Neither is a restored clone of the current main DB. That limitation is deliberate and must remain visible in deployment decisions.

## Dependencies

Inventory adds concurrency columns and transfer/receipt/return lineage. Material Request uses Inventory ISSUE lines and adds approval cycles/snapshots. Replenishment references material-request lines, warehouses, thresholds and stock receipt evidence. Maintenance usage references work/log pairs and issued stock lines. Auth/RBAC must exist before authenticated application testing; session validation precedes authorization and business transactions.

No Inventory Adjustment/Reversal workflow or DEV2 API is changed.
