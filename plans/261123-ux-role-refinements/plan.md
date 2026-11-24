# Project 2: coordinated UX and operational refinements

Preserve existing compact UX and prior uncommitted HKS work. User authorizes subagents and requested primary room reset/demo cleanup; backup and exact impact review precede mutations. No unrelated role grants/schema changes or publishing.

- [x] Account implementation: generated employee codes, password8/checklist/Vietnamese validation, branch creation assignment, phone/department filter, self profile. Isolated API validation passed.
- [x] Workday implementation: multi-person assignment, apply month, holiday handling and scoped configuration. Calendar anchors verified for 2026 and 2027; annual leave decisions remain with hotel.
- [x] Operations implementation: room grouped assets, condition bar, daily/weekly/monthly task stats, Vietnamese labels/config. API validation passed.
- [x] Parent: shared focus/login/pagination, Reception presentation, structure tree and branch details, catalog codes/duplicate validation/permissions hiding guard.
- [x] Backup and preview primary reset/demo deletion; user explicitly approved readiness baseline view and temporary trigger suspension. Applied 2026-11-23 10:18 local: 28 demo works, 10 demo documents removed; 96 rooms Ready; 0 disabled triggers/negative balances. Evidence artifacts/ux-primary-reset-verification.json. 57 users/131 grants retained.
- [x] Integration, frontend/backend builds, 30 API groups, 43 .NET tests (3 skipped), 17 Workday checks, 5 integrated browser checks and review completed. Individual Compact suites passed. Full legacy npm suite remains failing; combined UX run has intermittent load/overflow failure, separately rerun pagination passed. Limits explicitly reported.

Runtime at handoff: API PID46772 port5768 on HotelOpsCompactDB; Vite PID6424 port5186. Fixture API5192 stopped, temporary preview5188 stopped. Delegates completed. MCP agent dispatcher failed Windows command length; direct Codex CLI via approved escalation was used for scoped agents.
