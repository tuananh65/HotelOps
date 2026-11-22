---
title: Staffing V3 approved implementation
date: 2026-11-22
summary: "Applied54 personnel profiles with39 inactive accounts; backup, rollback and role-scope verification passed."
---

# Staffing V3 approved implementation

Implemented approved Staffing V3 on HotelOpsCompactDB after COPY_ONLY/CHECKSUM backup, isolated restore, transaction failure rollback rehearsal, Admin identity recovery and 31 passing fixture API checks. Primary now has54 users:15 unchanged-active accounts with normalized identity and39 INACTIVE/no-password accounts;42 role assignments added,3 revoked,78 memberships added, no grant definitions/schema changes. Owner global ALL and old sessions preserved. Primary26 API checks, UI6, backend26 and frontend4 automated tests passed; one worker test skipped. First fixture failures were existing active timers and room eligibility; corrected test preconditions via fixture APIs, no application weakening. Pending: safe provisioning/activation, DVCT scheduling/storekeeper staffing, coverage/rest rules and optional authority cleanup. Single-agent; no publication.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
