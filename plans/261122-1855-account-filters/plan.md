# Account directory filters

Current request: move Create account beside the filters and replace the status selector with detailed, compact filters. Single agent, sequential delivery.

- [x] Inspect account UI, user listing contract, membership and role validity, existing authorization and database mappings.
- [x] Extend the existing USER_VIEW-protected read response with current employment and role labels. No schema, permission or business-data changes.
- [x] Add a collapsible branch-of-employment / department / role panel; keep search, reset, count and create permission checks.
- [x] Build FE/BE, run relevant tests, verify live SQL-backed API and desktop/mobile UI after backup; review and report.

Filter semantics: branch and department use current EMPLOYMENT memberships, not mere branch access. Roles must be active, assigned, unrevoked and unexpired. Combined department/branch filters match the same employment; role scope matches that branch or is global.

Runtime: retain Vite 5186 (node 6424, npm 9324, launcher 35028) and API 5768 (dotnet 42448). No disposable background processes at recovery. Process-management rule file is absent. No pending approval or PR.

Verification in progress: FE build passed; dotnet build passed with zero warnings/errors; dotnet test passed 26, skipped 1 unrelated worker fixture. API restarted on 5768 with PID 5304. Verified backup precedes test authentication. SQL snapshot has 56 users, 53 current employment memberships and 56 role assignments. API matches the independent SQL snapshot. Initial harness errors were SQL JSON omission of empty employment arrays and Playwright exact label matching (combobox accessible names verified and used instead). No product workaround or relaxed filter assertions.

Completed: 8/8 account filter checks and 12/12 existing UI regression checks passed. See `plans/reports/test-261122-1855-account-filters.md`. Final image capture disables CSS animation during screenshots. Retain Vite 5186 and API 5768; test browsers close in finally; no extra service started.
