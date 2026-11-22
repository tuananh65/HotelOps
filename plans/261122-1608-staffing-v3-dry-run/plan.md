# Staffing V3 dry-run

Scope: use Word V3 requirements and Excel V3 targets; compare 15 existing users, plan 39 new users in HotelOpsCompactDB. Single-agent, read-only database. No SQL writes (including rollback-based simulated INSERT), login/API authentication, account activation, permissions, schema, seed, real schedule, notifications/email, commit/push/deploy. No backup needed for SELECT-only diagnostics.

- [x] Read Word requirements; record 54 total target, 16 DVBV +23 DVCT additions, minimum staffing by role, unresolved role mappings425/432/433.
- [x] Read every Excel sheet, preserve source row identifiers; verify 15+39 matches roster54 and staffing sheet.
- [x] Snapshot live user identities, roles/grants, organization, collation/indexes and dependent records using SELECT only; include soft-deleted/reserved accounts in collision checks.
- [x] Compare identifiers using actual SQL collation, check duplicates within targets and against live DB, validate role/branch/department mapping and ID preservation.
- [x] Build non-executable dry-run manifest with before/after, planned changes/counts, permission impact, dependency risks, rollback/preconditions and staffing coverage.
- [x] Deliver report with required status fields and exact approval scope; do not apply changes.

Sources: C:/Users/Hax/Downloads/HotelOps_Codex_Staffing_Handoff_V3.docx and HotelOps_Nhan_Su_Gia_Lap_Chuan_Hoa_De_Duyet_V3_Codex.xlsx. Initial ExcelJS read failed (workbook.sheets undefined); inspect archive and read XML directly without modifying source or library.

Completed: namespace-agnostic XML extracted all 10 source sheets. SQL collision counts all zero; 54 physical ID mappings verified. Owner438 global role contradicts Excel DVBV claim. Three role changes await approval; bootstrap424 and activation/password/session policy remain gated. Missing DVCT scheduler/Storekeeper and daily HK/ENG/STORE coverage recorded. No user question pending; these are documented future implementation gates, not authorization to act.

Deliverables: plans/reports/audit-261122-1608-staffing-v3-dry-run.md; docs/database/compact-v2/staffing-v3-dry-run/HotelOps_Staffing_V3_DryRun.xlsx and staffing-v3-manifest.json. Workbook round-trip 3911 cells/11 sheets PASS. Source hashes unchanged. Users/assignments/memberships/master references identical between 09:12:17Z and09:18:26Z. No source/DB/Git writes, login, emails or new background processes. Build/API tests not run for read-only scope.
