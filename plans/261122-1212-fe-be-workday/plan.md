# HotelOps — FE/BE integration and Workday

## Authorized outcome
Connect the actual React frontend to Compact v2 bearer APIs. Add weekly Workday planning and own Start/End; improve operational flows and responsive UI. Preserve legacy database and V10 standalone prototype. Single agent; no commit/push/deployment.

User approved three additional tables, maximum 40. Maintenance lead/staff and storekeeper: morning 08:00–11:30, afternoon 13:00–17:00; capacities 1/3/1. HK supervisor/staff and reception: 00–08, 08–16, 16–24; capacities 1/4/1. Start only on assigned current local date, without clock-hour restriction. One active shift per person. Task timers independent.

## Phases / todo
- [x] Inspect SRS DOCX, Compact entities, access and API conventions; preserve pending draft decisions.
- [x] Confirm table budget and scheduling policy with user.
- [x] Backup actual Compact DB; record verification and baseline.
- [x] Add three-table SQL upgrade, explicit EF mappings, transactional Workday service and scoped endpoints.
- [x] Connect FE authentication, branch scope, room/work/document flows and weekly calendar to actual APIs.
- [x] Run .NET build/tests, FE build/tests, real API and browser workflows on isolated data; review regressions.
- [x] Apply verified upgrade to current Compact DB, document results, failures and unverified behavior.

## Acceptance
No more than 40 tables; no change to legacy DB. Backups before data/schema mutation. No hardcoded IDs/implicit permission escalation. Capacity, overlap, branch/department membership, stale versions, ownership, current local date and audit enforced server-side. FE displays real data and permissions, handles loading/errors/conflicts and prevents duplicate submissions. No mock role switching. Preserve cleaning/inspection/readiness, inventory approval/posting and independent verification rules.

## UI brief (AgentKit ak-frontend-design)
Register: Product. Scene: hotel staff on desktop and phones completing practical daily work. Direction: V10 operational workspace with next valid actions. Color: restrained teal; background #f4f7f6, text #203b38, accent #21665e. Type: Segoe UI/system, hierarchy by weight and size. Signature: actionable room grid and weekly capacity calendar. Dials: variance 3, motion 2, density 6.

## Final checkpoint ? 22 November 2026

Implementation and defined validation completed. Primary HotelOpsCompactDB is schema 2.1.0 with 40 tables, 160 FKs, 82 checks, 27 triggers and 6 views. COPY_ONLY/CHECKSUM backups verified before schema changes. The externally added empty sysdiagrams table was removed only after explicit user approval; no legacy DB changes.

Backend build: zero warnings/errors. Unit tests: 26 pass / 1 optional worker skipped; worker integration separately passes. Current backend regression: 127 pass. Workday API: 14 groups pass. FE workflow: 4 pass. Browser: all 8 groups verified on fixture D across resumed runs; failures and fixes retained in report. Final visual audit: 30 cases, zero errors/warnings. Four UI states tested against primary: hover, empty search, network failure/retry, Escape. Final FE build passes.

Preview remains at http://127.0.0.1:5178 with API5768 on primary (owned PID15860). Vite PID3928, npm parent24564. Test API5769 stopped. Other pre-existing dev servers untouched. No commit/push/deployment.

Report: ../reports/test-261122-1319-fe-be-workday.md. Technical guide: ../../docs/database/compact-v2/WORKDAY-INTEGRATION.md. Advanced legacy UI parity, all document browser permutations and SRS OPEN decisions are not represented as completed. Legacy npm test baseline timed out; Compact tests have separate explicit commands. No pending approval.
