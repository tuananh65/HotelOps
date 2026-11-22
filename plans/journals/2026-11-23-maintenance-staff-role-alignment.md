---
title: Maintenance Staff role alignment
date: 2026-11-23
summary: Implemented permitted Maintenance Staff workflow with isolated API/browser verification and explicit contract blockers
---

# Maintenance Staff role alignment

## Outcome
Implemented Project 2 Maintenance Staff presentation, personal maintenance data and existing execution workflow. Preserved the accepted compact UX and kept Single-Agent execution.

## Root causes and changes
The generic workspace omitted maintenance-specific personal KPI and review/rework context. The actual Staff role has 13 grants, including INCIDENT_MANAGE and IR_PROCESS, but lacks INCIDENT_CREATE and IR_CREATE. Coarse Incident management could expose coordination actions; assignment candidates and handover now require existing coordination capability. Internal Request routing no longer treats a regular assignee as department authority. Maintenance evidence requires the current assignee, and MR source links validate ownership. Review/reopen notifications now reach the actual technician.

## Verification
Backend build: zero warnings/errors. Solution tests: 28 passed, 3 conditional skips. Staff fixture: 27 grouped checks, final database HotelOpsCompactApiTest4eabd21e611e. Real browser: 30 views, 10 successful browser writes plus Lead reopen through fixture API, no page errors/failed API. Extended API regression: 128 passed. Lead regression: 33 passed, PM worker integration 1 passed. Shared UI suites and four prior role regressions passed. All test writes stayed in isolated backed-up fixture databases.

## Limits and decisions
Decline has no current transition/API and remains blocked. Incident and IR creation remain hidden/403 because their grants are absent. No invented statuses, permission grants, schemas, primary fixture writes, stock consumption or duplicate lifecycle. No subagents, commits, pushes or deployments. Keep existing .NET User Secrets; no secrets copied into artifacts.

## Environment and follow-up
Restored primary Project 2 API on port 5768, worker=false. Kept existing Vite 5186; fixture API processes stopped in finally. Product decisions are needed only for a future decline contract or explicit permission revision. Full handoff and file list are in plans/reports/implementation-261123-0332-maintenance-staff-alignment.md.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
