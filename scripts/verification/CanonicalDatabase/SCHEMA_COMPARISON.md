# Combined schema comparison

The base release creates 72 tables. DEV2 adds four Inventory detail/receipt tables, two Material Request approval tables, three Replenishment tables and one Maintenance Material Usage table, producing **82 tables** matching 82 EF entity types.

| Contract | Main additions |
|---|---|
| Inventory 20261019 | stock_transfer_receipts, stock_transfer_receipt_lines, stock_receipt_line_details, stock_return_line_details; seven aggregate rowversions; lineage/indexes |
| Material Request 20261020 | material_request_approval_cycles, material_request_approval_line_snapshots; request rowversion, receiving location and closure fields, active lines, approval/fulfillment lineage, status/quantity checks, filtered unique indexes |
| Replenishment 20261021 | material_replenishments, material_replenishment_lines, material_replenishment_receipt_lines; rowversion, coverage uniqueness, warehouse ownership and receipt lineage |
| Maintenance Usage 20261022 | maintenance_material_usages; composite work/log FK, stock issue reference, quantity/factor checks and capacity/history indexes |
| Canonical reference contract | One additional CHECK on reference_values, restricted to PRIORITY; no SEVERITY change |

Final expected totals: **82 tables, 238 FKs, 54 CHECK constraints, 80 defaults, 201 indexes, nine rowversion columns**. Index count includes primary/unique indexes once each and excludes heaps/hypothetical indexes; it is not a count of index-column rows. Older release README counts use a different historical inventory and are not evidence for this combined release.

Schema evidence compares fresh/RBAC-upgrade/Auth-upgrade column type, length, precision/scale, nullability, identity/computed/collation, default definitions; FK column order, references, actions and enabled/trusted state; index keys/directions/includes, uniqueness and filters; and CHECK definitions/state. System-generated key names are normalized to avoid random suffix differences. The three paths must compare equal. This is a schema comparison, not preservation testing of a restored business dataset.

The EF probe forces model validation; verifies every mapped column's SQL type/nullability; checks all unique keys and 238 FK property/principal column mappings; checks declared index keys/uniqueness; verifies nine generated concurrency tokens; rejects shadow properties; and executes a real read for every entity. Snapshot comparison covers additional SQL defaults/checks/includes/filters beyond the focused EF metadata checks. EF checks do not independently prove every index sort/filter definition or every FK delete action against EF semantics.

The current combined model also infers **165 nonunique indexes by EF convention** which SQL does not contain exactly. These are recorded individually as advisories, using EF configuration-source metadata; no explicit index/unique-key mismatch is downgraded. They do not prevent materialization or optimistic concurrency. Do not add all of them automatically: review existing composite-index prefix coverage and query plans with DEV2 first.
