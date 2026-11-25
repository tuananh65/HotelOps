# Recovered roster and global scope verification — 25 November 2026

Implemented exact dual alias check for inactive DH: DEPARTMENT_HEAD and DEPT_MANAGER.
Fresh TEST backup CHECKSUM/RESTORE VERIFYONLY succeeded. Recovered roster runtime rerun: 6 PASS at 2026-11-25T12:26:14Z. Ten original Maintenance Staff remain active (five per provenance branch); credentials/account-state comparison from existing read-only SQL artifact, actual admin API and Edge roster checks passed. No passwords reset.

Added standalone global role GET audit and exact-run OWNER session setup/cleanup to the existing TEST fixture. Latest global-role-read-scope-results.json: 13 PASS. SYS_ADMIN and OWNER current grants obtained from auth/me; actual GET room reports and request lists verified independently against effective ALL grants for every active branch. SYS_ADMIN has infrastructure administration grants, not REPORT_VIEW/IR_VIEW, so corresponding reads correctly return 403. OWNER has only VIEW/EXPORT grants; room reports return scoped data. No management endpoints were invoked to prove write rejection; grant inspection is not destructive-endpoint testing.

Earlier failed attempts assumed SYS_ADMIN had REPORT_VIEW; corrected the test to follow actual grants without changing role permissions. One overlapping rerun was refused by existing fixture ownership guard; original run cleaned successfully before final execution. Latest run PASS, cleanup succeeded, fixture and process metadata removed. Port 5779 untouched. No operational DB writes, role changes, password changes, or email.

Files: scripts/verification/v2-recovered-roster-runtime.mjs, scripts/verification/v2-invitation-fixture.ps1, scripts/verification/v2-global-role-read-scope.mjs. Evidence: artifacts/restructure-v2/recovered-roster-runtime-results.json and global-role-read-scope-results.json.

Validation: node --check both JavaScript runners PASS; PowerShell AST syntax PASS. No C# changed; build/test not rerun for this verification-only change. Existing eight-actor foreign-branch checks remain separate evidence; these global-role checks do not establish all 47 E2E families PASS. No unresolved question.
