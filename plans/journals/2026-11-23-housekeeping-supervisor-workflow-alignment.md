---
title: Housekeeping supervisor workflow alignment
date: 2026-11-23
summary: "Seven scoped HK workspaces, inspection/rework guards and immutable checklist versions"
---

# Housekeeping supervisor workflow alignment

Implemented seven permission-gated HKS groups while preserving the existing compact UX, pending-room intake and eleven real CompactDB grants. Added scoped dashboard, staffing/shifts, inspection/defect queues, immutable checklist versions and room workflow reads.

The repeated-failure regression reproduced a real bypass: an old submitted rework could admit another inspection after a newer failed review. Guards, queue and dashboard now require a submission not superseded by a later failure. A subsequent correction is allowed without parallel execution or deleting review history. Defect resolution checks DEFECT_RESOLVE plus branch/root/turnover and independent review. Operational blocks continue preventing READY after inspection passes.

Final validation: SQL fixture HotelOpsCompactApiTest7aeefa946273 passed 51/51 groups. HKS browser passed 24 groups at three viewports, including combined cleaning/block filters, exact-parent inspection creation/assignment and checklist versioning. Intake, Reception, HK Staff, OM and shared UX regressions passed. Frontend/.NET builds passed; .NET unit 28 passed/3 conditional skipped, workflow unit 5 passed. Browser APIs are mocked; SQL/RBAC evidence is separate. Screenshots wait for loaded data.

Published templates remain immutable under the existing trigger: new versions are supported, activation/deactivation is a contract blocker. No primary test writes, schema/grant/Staffing changes or Git publication. Local primary API restored at port 5768, PID 3144, health ok; Vite 5186 HTTP 200; fixture listeners stopped.

Report: plans/reports/implementation-261123-0850-housekeeping-supervisor-alignment.md. Initial CLI stdin journal text lost Vietnamese characters through the Windows native pipe; this local file was rewritten as UTF-8 ASCII-safe English and validated. AgentWiki publish skipped.

> Historical work record - not durable authority. Prefer docs/specs/ADRs for current decisions.
