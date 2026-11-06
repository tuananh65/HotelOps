# Known issues and verification limits

- A01/A02 MISSING_EXECUTABLE_SOURCE. UC029 candidate PROVISIONAL_NOT_ACCEPTED; shared/production OFF. No accepted shared dispatcher or production notification acceptance.
- C01 intercepts armed EF commits; direct provider Commit/CommitAsync, SQL COMMIT and other connections bypass it. UC029 uses only reviewed EF owner and enlisted constant SQL; no universal external-write protection.
- Commit/cancellation SQL component simulates before/after provider errors with auth doubles. Actual network partition/SQL process termination and recovery DB-outage paths not proven.
- No actual Tester execution/acceptance; all workbook statuses NOT_RUN. Dedicated inactive room/source-deletion scenarios are manual specifications, not invented developer PASS results. Credentials/shift need owner provision.
- FS010 full run01 failed 0/12 on Node public CA configuration; run02 11/12 due old expected staging500 vs new controlled503. Read FULL_SUITE_REPEATABILITY.md for subsequent full runs; do not aggregate partial evidence.
- Build/test sandbox attempts hit EPERM; helper builds while API/gateway/compiler held DLLs failed, then rerun sequentially. Preserve these validation limitations; no unrelated code changed to fix them.
- Inherited NU1900 vulnerability metadata unavailable, Vite React Router use-client/chunk-size warnings, EF20602 view-index warnings, NO_COLOR/FORCE_COLOR and Git LF/CRLF. No clean vulnerability scan claim.
- SRS draft wording about room status/history differs from confirmed additive readiness/current-cycle implementation. Shared review still needed. Reception/Department Head pending permissions untouched.
- UC030/031/033/034/035/036 NOT_COVERED. UC032 regression only; UC037 partial boundary/UI/API; UC038 boundary only/inactive controller. No full FE04 merge readiness or percentage claim.
- Retained trial only; scripts refuse overwrite and recovery populated evidence. SchemaSafety proof verifies rollback restores a temporary table rename and populated recovery refuses 51917; no destructive recovery/drop executed. Do not rebuild live binaries while owned processes run.
