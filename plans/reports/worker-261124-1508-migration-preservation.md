# P0 migration preservation audit

## Scope and evidence

- Original backup: verified COPY_ONLY/CHECKSUM backup recorded in `artifacts/workday/backup-HotelOpsCompactDB.json`, captured before P0 migration at 2026-11-24 05:46 UTC.
- Restored that existing backup into new `HotelOpsCompactWorkdayTestP0Baseline261124`, then set this baseline READ_ONLY. No overwrite, reset, drop or Production write occurred.
- Compared with `HotelOpsCompactWorkdayTestP0261124`. Script: `scripts/verification/p0-preservation-audit.ps1`. Safe count-only evidence: `artifacts/p0-workflow/preservation-audit.json`.
- Credentials are compared inside SQL only. No credential values, hashes, salts or tokens are emitted or written to artifacts.

## Verified results

- Users: **57 original → 77 total**, exactly **20 added TEST users**. All 57 original IDs and original user rows remain unchanged. Original credential differences: **0**.
- All primary keys in all 40 baseline tables remain present, except the deliberately replaced role-permission mappings. Those mappings are checked separately against the migration audit snapshot.
- Membership rows: **114 → 164**, none removed; 10 original rows changed (employment validity cutover).
- User role assignments: **56 → 92**, none removed; 16 original rows changed (role revocation).
- Sessions: **330 → 350**, none removed; 138 original rows changed (session revocation).
- Original audit history **437 rows**, room events **123**, work events **21**, workday events **37**, asset events **1**, work intervals **3**, inspection findings **1**: no original row missing or modified.
- Original 7 work items, 3 work assignments, 28 workday assignments, 96 rooms and 6 assets remain unchanged.
- Original permission definitions **67** remain unchanged; current definitions **72**.

## Review boundaries

- The migration deliberately removed/replaced 92 original role-permission rows. This is approved permission revocation, not business-history deletion. The script requires all 134 original grants to be represented in `P0_ROLE_MIGRATION.before_json` before declaring retention verified.
- Other changed original rows: 3 roles, 8 shift templates, 3 inventory policies, 1 warehouse. Counts align with migration/default/deactivation scope, but this audit does not independently prove every changed field conforms to the new business policy.
- Row comparison excludes SQL rowversion, legacy text/image and XML columns. Credential columns have their own SQL equality check. Tests run while the TEST application is active, so newly added runtime counts can increase after capture; original-row retention is the core assertion.
- PowerShell syntax parsing passed; `node --check scripts/verification/p0-runtime-runner.mjs` passed. No C# or frontend source modified; no build required for this verification-only module.
- No orphan server or watcher was created. The baseline database remains available read-only for subsequent checks.

Unresolved: detailed field-level policy review of intentional migration changes remains Integration Owner responsibility; no missing original business records or changed original credentials were found.
