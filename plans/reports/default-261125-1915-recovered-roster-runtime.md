# Recovered roster runtime — 25 November 2026

Implemented and executed `scripts/verification/v2-recovered-roster-runtime.mjs` using the owned invitation fixture on5778, email disabled, current `.artifacts/role-session-fix` binary and a fresh CHECKSUM/VERIFYONLY TEST backup. Inventory5779 was not touched.

Result: **6 checks PASS**, actual HTTP and Edge. Artifact: `artifacts/restructure-v2/recovered-roster-runtime-results.json`.

- Original provenance identifies ten recovered Maintenance Staff, five per branch. Existing read-only SQL comparison verifies credentials/status equal source10/10; this runner does not expose hashes or tokens.
- Actual role catalog contains active Maintenance Staff and one active OM role, no active Department Head role.
- API user directory retains each recovered person's active Maintenance Staff assignment in the original branch.
- Browser administrator directory displays Maintenance Staff for all ten recovered usernames, plus an active OM.
- Read-only browsing preserves all ten user/membership/role response records exactly.
- Screenshots: `recovered-roster-branch-413.png`, `recovered-roster-branch-414.png`, `om-consolidated-roster.png`.

Cleanup completed: temporary administrator session removed, API5778 stopped, zero synthetic accounts created. The shared fixture reports `activeAccounts:null` because SUM over zero rows is NULL; this is not an active account count. No recovered password login, reset, role mutation, email delivery or operational database write was performed. These limitations must remain explicit when attaching evidence to E2E01/02.

No C# source changed; build/tests were not rerun for this JS-only helper. Syntax verification: `node --check` PASS.

Unresolved questions: none.
