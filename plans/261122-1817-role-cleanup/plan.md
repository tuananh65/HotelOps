# Role data and UI cleanup

Explicit user scope: remove role code column, physically delete inactive roles from Compact DB, color and align Edit/Permissions buttons. Single agent; sequential work; no Git publishing.

- [x] Inspect actual roles, all foreign keys and triggers; preserve active assignments and grants.
- [x] Fresh verified backup, guarded transactional cleanup with before/after audit, verify database invariants.
- [x] Remove UI code column; align colored accessible action buttons.
- [x] Build, relevant UI checks and screenshots; document result.

Role codes remain stable internally. Vietnamese display descriptions must describe existing responsibilities, never add permissions. Keep user-facing Vite PID 41940 port 5186 and API PID 42448 port 5768; launcher 35444. No disposable process to stop. Prior UX plan is complete; no pending approval. No active PR work.

Result: deleted DEMO_MANAGER/DEMO_AUDITOR, their 47 grants and 3 revoked assignments. Preserved all 56 current assignments, 131 effective role grants, 56 accounts and 40 tables. Ten Vietnamese role descriptions; audit 253. Build and 4 workflow tests passed; live role UI/API and 3 viewport screenshots passed. See plans/reports/test-261122-roles-cleanup.md.
