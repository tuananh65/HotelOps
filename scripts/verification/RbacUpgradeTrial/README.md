# BE1 RBAC Upgrade Trial — 21/10/2026

Result: **PASS WITH NOTES**. Main database **not upgraded**.

## Environment and retained artifacts

- Server: `DESKTOP-JI8U50T\SQLEXPRESS`.
- Source: `HotelOpsDB`.
- Trial: `HotelOps_RBAC_Upgrade_Trial_20261021_380b6cc4b282`.
- Backup: SQL Server default Backup directory, file named after trial database with `.bak` extension. COPY_ONLY, CHECKSUM, NOINIT; unique new path; VERIFYONLY CHECKSUM passed.
- Restore used FILELISTONLY logical names and MOVE to separate trial `.mdf` / `_log.ldf` paths in the SQL Server data directory. No REPLACE, detach, SINGLE_USER, drop, or source upgrade.
- Backup stays outside repository. Trial database and backup retained.
- Connection is read privately from existing Development User Secrets, with environment override. Credentials/connection strings are never persisted by these scripts.

## Executed results

| Step | Result |
| --- | --- |
| Source/permissions/file-layout preflight | PASS |
| COPY_ONLY CHECKSUM backup | PASS |
| VERIFYONLY and restore to new database | PASS |
| Restored content compared to source baseline | PASS: counts and SHA256 fingerprints of all 72 tables |
| Trial dry run | PASS: final COMMIT replaced with ROLLBACK in separate trial copy; all table contents restored |
| Trial commit | PASS |
| Standard script 04 | PASS with legacy department warnings |
| Exact catalog comparison | PASS: roles, permission metadata/descriptions, grants/scopes; bidirectional EXCEPT |
| Approved personnel assertions | PASS |
| DBCC CHECKDB / CHECKCONSTRAINTS | PASS, no findings |
| Enabled/trusted FK/CHECK, enabled unique indexes | PASS |
| Source content after trial | PASS: all 72 table counts and SHA256 fingerprints unchanged |
| Task 01 and standard Release file SHA256 | PASS: unchanged |

The release script does not offer a built-in dry-run switch. Trial copies preserve its guards and transaction; only the dry-run copy replaces the final COMMIT with ROLLBACK. The existing data_scope column meant the separate schema pre-step did not add a column.

## Catalog and personnel

| Metric | Before | After | Target |
| --- | ---: | ---: | ---: |
| Roles | 11 | 12 | 12 |
| Active roles | 11 | 10 | 10 |
| Inactive roles | 0 | 2 | 2 |
| Permissions | 47 | 62 | 62 |
| Grants | 182 | 140 | 140 |
| DEPARTMENT grants | 45 | 0 | 0 |

Final scopes: ALL=8, BRANCH=81, OWN=51. HK_INSPECTOR and STAFF inactive. OWNER has only DASHBOARD_VIEW:ALL and REPORT_VIEW:ALL.

User 4 remains DH Branch 1; user 19 remains DH Branch 2. Other DH assignments in those branches deactivated by @SelectedDh. User 5 retains Storekeeper Branch 1; user 19's Storekeeper assignment deactivated. User 1 becomes OM Branch 1, with Branch 2 MANAGER access revoked. All personnel choices exist only in trial copies, never the shared Release.

## Preservation and schema

Only six tables changed: roles 11→12, permissions 47→62, role_permissions 182→140, user_roles 30→32, users 26→26, user_branch_access 28→28. The two added user_role rows are Inspector-to-Supervisor transitions from the release script. Selected DH department links are cleared by the release. Every user field other than department_id is fingerprint-identical, including account status/credential fields. Other 66 tables, including SQL sessions and all business history, retain exact counts and SHA256 content fingerprints. No business data was deleted.

Final schema has 72 tables, 201 FKs, 40 CHECKs, 78 defaults, 337 indexes. Static comparison with Full Schema found 0 differences across 712 column names/types/lengths/precision/scale/nullability. Table/column schema also matches pre-upgrade clone. This is not a full semantic comparison of every index/default/FK definition against script 01.

Source remains 11 roles, 11 active, 47 permissions, 182 grants, 45 DEPARTMENT grants; all source table fingerprints match initial baseline. Backend configuration and Task 01 files were not edited.

## Remaining notes

FB/LAU/SEC at Branch 1 and SEC at Branch 2 remain active. Direct FK references exist from users, warehouses, internal request types/requests, work categories, operational tasks and shifts. No unapproved department reassignment or deactivation was performed. These warnings require a separate business/data decision before upgrading the main database.

Authorization/API behavior is outside this trial. EF RolePermission still lacks DataScope; mapping changes require a separate task. Main database upgrade requires separate approval, tested restore and review of the exact personnel/data decisions. No build/test run was necessary because C# source was unchanged.

PowerShell tooling issues corrected during execution: connection-builder property assignment, a static check confusing string value `execute` with EXEC, and a nullability comparison variable. None required removing a SQL guard or changing shared Release code.

## Files and reruns

`Run-Trial.ps1` orchestrates stages. `Verify-Trial.ps1` is called by its Verify stage and relies on its helper functions. `trial-state.json`, `trial-before.json`, `trial-after.json`, `source-after.json`, `extra-baseline.json`, `schema-column-differences.json`, and `verification-result.json` store noncredential evidence. Trial SQL copies contain the explicitly approved trial-specific employee IDs and database name.

Do not rerun Restore or Upgrade on this completed trial. Read-only verification may be repeated:

```powershell
& ./scripts/verification/RbacUpgradeTrial/Run-Trial.ps1 -Stage Verify
```

No commit, push, PR, merge or cleanup/drop was performed.
