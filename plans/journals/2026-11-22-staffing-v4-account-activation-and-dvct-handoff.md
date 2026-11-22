---
title: Staffing V4 account activation and DVCT handoff
date: 2026-11-22
summary: 56 active test users verified; RBAC and40-table schema preserved; fixture and primary evidence retained.
---

# Staffing V4 account activation and DVCT handoff

## What happened
Owner approved Staffing V4 for the internal HotelOps test environment. Reset credentials for54 existing users, activated39 V3 records, created and activated DH002/MinhNQ1 and EMP050/KhangTD1 in DVCT. Primary now56 ACTIVE,0 INACTIVE,40 tables,160 trusted FKs,67 permissions and178 grants unchanged. Batch C7046EC2-244C-4EAA-8799-B9E946422659 committed2026-11-22 10:19UTC; audit249-251. No application source/schema edits or Git publication.

## Evidence and decisions
Fresh canonical database snapshots including rowversions, exclusive transaction guards, independently salted existing Identity hashing, stamp rotation and33 session revocations. No credential values/hash/token logged. Restored backup, injected rollback and stale-snapshot rejection, idempotency, Admin credential recovery and old-session401 all verified. Existing Owner global12 grants and Admin5 system grants preserved. DH employmentHK follows existing record structure while SHIFT_MANAGE remains branch-wide. Department-authority decisions remain unchanged.

Fixture68 API checks and7 browser checks passed. Primary61 API checks include56 actual logins and exact role/grant/branch comparisons. Backend build0 warnings/errors,26 unit tests passed with1 worker integration skipped; frontend build and4 unit tests passed. Primary Workday assignments remain0. All V4-owned API/Vite processes stopped.

## Failures resolved
Windows PowerShell5.1 UTF-8 BOM, JSON array count, LASTEXITCODE semantics, SQL DECLARE initialization ordering and shared log file reads required tool-script corrections. All fixed or explicitly documented before handoff; none caused partial primary mutation. No business-policy change was needed.

## Limits and follow-up
Separate QA worker integration was not run. Existing DVCT warehouse label still says Da Nang; outside staffing scope. Existing department-authority mappings retained; no real week schedule authorized. Full report: plans/reports/test-261122-1659-staffing-v4-implementation.md. Backups and redacted evidence: artifacts/staffing-v4. AgentWiki publish skipped.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
