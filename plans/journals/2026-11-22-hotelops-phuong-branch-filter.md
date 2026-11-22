---
title: HotelOps Phuong branch filter
date: 2026-11-22
summary: Fix branch-access-only accounts missing from account filters
---

# HotelOps Phuong branch filter

Investigated PhuongDH1 against live SQL. The active Bui Vien Operation Manager account has BRANCH_ACCESS only, while the directory/filter used EMPLOYMENT only. Added a distinct active branch-scope DTO to the directory and used it for branch-only filtering, preserving employment-based departments and same-branch role matching. No role/membership/schema changes. Verified backup before real login tests. Old API reproduced the failure; patched API and actual browser passed username, combined filter, negative branch/department and reset cases. FE and BE builds passed; first BE build hit a running-DLL lock, resolved by stopping only the verified owned API then restarting PID 31332 on 5768. Dotnet tests: 28 passed, 2 fixture skips. Report: plans/reports/debug-261122-2037-phuong-branch-filter.md. AgentWiki publish skipped.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
