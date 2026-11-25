# E2E-07 role-change verification probe

- Added `scripts/verification/v2-role-revocation-browser.mjs`; `node --check` PASS.
- Helper receives invitation fixture, activated synthetic account, current password, real request client and artifact directory. It logs in two separate Edge contexts, grants HK_STAFF via actual admin API, expects both old tokens401 and browser logout; logs in again, revokes assignment through actual admin API and repeats assertions. No credential values saved.
- Integrate into `v2-invitation-runtime.mjs` after recovery scenario using account `a` and `recoveredPassword`, or after the final SMTP assertion. Use an optional flag and existing test wrapper. Fixture cleanup remains runner-owned. Role lookup uses catalog code, no hardcoded role/branch IDs.
- Runtime NOT RUN. No TEST or operational mutations, no C# changes. Separate5778 fixture exists, but this handoff does not start it or claim fresh backup/runtime authorization.
- Potential product gap: `UserService.AssignRole`/`RevokeRole` do not explicitly revoke sessions or rotate SecurityStamp. `Atomic` only commits; DbContext is generated and has no SaveChanges hook shown. Existing runtime role test directly changes SQL and only checks visibility. Database trigger behavior has not been confirmed; do not declare defect proven until helper runs. If helper receives200 from old `/auth/me`, preserve failure and implement session revocation transactionally for successful role changes.
- DB session timestamp and role-change SignalR evidence are not included by this helper and remain separate checks; no PASS claim for them.

Unresolved questions: none. Parent must run the probe and handle any observed failure before E2E-07 can be upgraded.
